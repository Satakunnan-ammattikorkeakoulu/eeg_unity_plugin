// TODO: If the session is for any reason not released before the parent game object is destroyed,
//       session will continue and can not be restarted or recreated before application restart.
// Does this need to inherit MonoBehaviour and use OnDestroy()?

using System;
using System.IO;
using System.Timers;
using brainflow;
using Timer = System.Timers.Timer;

namespace Plugins.Restfulness
{
    public class Predictor
    {
        private bool _isStreaming;
        private double _restfulnessScore;
        private bool _predictEventSubscribed;
        private bool _firstPrediction = true;

        private readonly BoardShim _boardShim;
        private readonly MLModel _model;
        private readonly int _samplingRate;
        private readonly int[] _eegChannels;
        private readonly Timer _timer = new();
        private readonly int _predictionInterval;
        private readonly int _dataCount;

        /// <value>
        /// Score that indicates how restful the user is. The higher the score, the more restful the user is.
        /// The value is provided through the OnRestfulnessScoreUpdated event, but it is also possible to create a different way to get the score via the public getter.
        /// Value is between [0, 1].
        /// </value>
        public double RestfulnessScore
        {
            get => _restfulnessScore;
            private set
            {
                _restfulnessScore = value;
                OnRestfulnessScoreUpdated?.Invoke(_restfulnessScore);
            }
        }

        /// <summary>
        /// Initializes the predictor with the provided boardId and prediction interval.
        /// </summary>
        /// <param name="boardId">Board ID of the board.</param>
        /// <param name="predictionInterval">Optional: The interval in milliseconds at which the predictor will make a predictions. Default is 2500</param>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="predictionInterval"/> is less than 500. Any less than 500ms is not enough data to make a prediction.</exception>
        /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board was not initialized properly.</exception>
        public Predictor(BoardIds boardId, int predictionInterval = 2500)
        {
            if (predictionInterval < 500) throw new ArgumentException("Interval must be 500ms or greater.");

            var inputParams = new BrainFlowInputParams();
            _boardShim = new BoardShim((int)boardId, inputParams);
            _samplingRate = BoardShim.get_sampling_rate(_boardShim.get_board_id());
            _eegChannels = BoardShim.get_eeg_channels(_boardShim.get_board_id());
            _boardShim.prepare_session();

            var modelParams = new BrainFlowModelParams((int)BrainFlowMetrics.RESTFULNESS,
                (int)BrainFlowClassifiers.DEFAULT_CLASSIFIER);
            _model = new MLModel(modelParams);
            _model.prepare();

            _predictionInterval = predictionInterval;
            _dataCount = (int)Math.Round(_samplingRate * (_predictionInterval / 1000.0));
        }

        /// <summary>
        /// Triggered when the RestfulnessScore is updated by the predictor.
        /// The event provides the updated RestfulnessScore as a parameter to any registered listeners.
        /// The score value is between [0, 1].
        /// </summary>
        public event Action<double> OnRestfulnessScoreUpdated;

        /// <summary>
        /// Starts the stream and invokes the Predict method at given interval.
        /// </summary>
        /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board is not initialized.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already running.</exception>
        public void StartSession()
        {
            if (_isStreaming) throw new InvalidOperationException("Cannot start stream, it is already running.");
            _boardShim.start_stream();
            _isStreaming = true;
            _timer.Interval = _predictionInterval;
            _timer.Elapsed += Predict;
            _timer.Start();
        }

        /// <summary>
        /// Stops the stream and releases the BrainFlow and ML sessions.
        /// </summary>
        /// <exception cref="BrainFlowError"></exception>
        public void StopSession()
        {
            StopStream();
            _boardShim.release_session();
            _model.release();
        }

        /// <summary>
        /// Enable BrainFlow logging for debugging purposes. Files will be saved in the log folder in the current directory.
        /// Naming convention: bf_{yyyy-MM-dd_HH-mm-ss}.log and ml_{yyyy-MM-dd_HH-mm-ss}.log
        /// </summary>
        /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Log file is locked for writing.</exception>
        public static void EnableDevLogging()
        {
            var logPath = Path.Combine(Environment.CurrentDirectory, "log");
            var timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
            var bfLog = Path.Combine(logPath, $"bf_{timeStamp}.log");
            var mlLog = Path.Combine(logPath, $"ml_{timeStamp}.log");
            BoardShim.set_log_file(bfLog);
            MLModel.set_log_file(mlLog);
            MLModel.enable_dev_ml_logger();
            BoardShim.enable_dev_board_logger();
        }

        /// <summary>
        /// Stops the stream and cancels the Predict method invoke.
        /// </summary>
        /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board is not initialized.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the stream is not running.</exception>
        private void StopStream()
        {
            if (!_isStreaming) throw new InvalidOperationException("Cannot stop stream, it is currently not running.");
            _boardShim.stop_stream();
            _isStreaming = false;
            _timer.Elapsed -= Predict;
            _timer.Stop();
        }

        /// <summary>
        /// Gets two times the prediction interval worth of data from the device and makes the prediction of the users restfullness state. Higher value means more rested state.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Predict(object sender, ElapsedEventArgs e)
        {
            double[,] data;

            if (_firstPrediction)
            {
                _firstPrediction = false;
                data = _boardShim.get_current_board_data(_dataCount);
            }
            else
            {
                var firstData = _boardShim.get_board_data(_dataCount);
                var secondData = _boardShim.get_current_board_data(_dataCount);
                data = ConcatenateData(firstData, secondData);
            }

            // Calculate avg and stddev (in that order) of band powers across all channels. Bands are 1-4, 4-8, 8-13, 13-30, 30-50 Hz.
            // The last parameter applies the following filters in order:
            // Band stop: 48 - 52 Hz, Butterworth, order 4
            // Band stop 58 - 62 Hz, Butterworth, order 4
            // Band pass: 2 - 45 Hz, Butterworth, order 4
            var bands = DataFilter.get_avg_band_powers(data, _eegChannels, _samplingRate, true);

            var featureVector = bands.Item1;
            // The result type is double[] but it contains only one element.
            RestfulnessScore = _model.predict(featureVector)[0];
        }

        /// <summary>
        /// Helper function to concatenate the two halves of the EEG data.
        /// </summary>
        /// <param name="first">First half of the EEG data</param>
        /// <param name="second">Second half of the EEG data</param>
        /// <returns>Concatenated double[,] of the EEG data</returns>
        /// <exception cref="ArgumentException">Thrown if the halves don't match in row dimension</exception>
        private double[,] ConcatenateData(double[,] first, double[,] second)
        {
            var firstRows = first.GetLength(0);
            var firstCols = first.GetLength(1);
            var secondRows = second.GetLength(0);
            var secondCols = second.GetLength(1);

            if (firstRows != secondRows)
                throw new ArgumentException("Data must have the same number of rows.");
            var result = new double[firstRows, firstCols + secondCols];
            for (var i = 0; i < firstRows; i++)
            for (var j = 0; j < firstCols; j++)
                result[i, j] = first[i, j];
            for (var i = 0; i < secondRows; i++)
            for (var j = 0; j < secondCols; j++)
                result[i, j + firstCols] = second[i, j];
            return result;
        }
    }
}