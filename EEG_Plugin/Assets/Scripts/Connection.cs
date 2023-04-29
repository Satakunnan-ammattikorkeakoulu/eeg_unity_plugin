using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using brainflow;
using brainflow.math;

public class Connection : MonoBehaviour
{
    private BoardShim _boardShim = null;
    private int _samplingRate = 0;
    private int[] _eegChannels = null;
    private BrainFlowInputParams _inputParams = new BrainFlowInputParams();
    private BrainFlowModelParams _modelParams = new BrainFlowModelParams(
        (int)BrainFlowMetrics.RESTFULNESS,
        (int)BrainFlowClassifiers.DEFAULT_CLASSIFIER);
    private MLModel _model = null;

    public BoardIds boardId;
    public double prediction;
    
    // Start is called before the first frame update
    private void Start()
    {
        // Enable debug logging
        BoardShim.set_log_file("brainflow_log.txt");
        MLModel.set_log_file("MLModel_log.txt");
        BoardShim.enable_dev_board_logger();
        MLModel.enable_dev_ml_logger();
        
        // Prepare the board and the ML model
        _model = new MLModel(_modelParams);
        _model.prepare();
        _boardShim = new BoardShim((int)boardId, _inputParams);
        _samplingRate = BoardShim.get_sampling_rate(_boardShim.get_board_id());
        _eegChannels = BoardShim.get_eeg_channels(_boardShim.get_board_id());
        _boardShim.prepare_session();
        _boardShim.start_stream();
    }

    // Update is called once per frame
    private void Update()
    {
        if (_boardShim.get_board_data_count() > _samplingRate * 5)
        {
            var data = _boardShim.get_board_data();
            foreach (var i in _eegChannels)
            {
                DataFilter.perform_bandpass(data, i, _samplingRate, 0.5, 40.0, 4, (int)FilterTypes.BUTTERWORTH, 0.0);
                DataFilter.perform_bandstop(data, i, _samplingRate, 49.0, 51.0, 3, (int)FilterTypes.BUTTERWORTH,
                    0.0);
            }
            Tuple<double[], double[]> bands = DataFilter.get_avg_band_powers(data, _eegChannels, _samplingRate, true);
            var featureVector = bands.Item1;
            prediction = _model.predict(featureVector)[0]; // This is what we want to output in Unity
            Debug.Log("Restfulness score: " + prediction);
        }

    }
    private void OnDestroy()
    {
        if (_boardShim != null)
        {
            try
            {
                _boardShim.stop_stream();
                _boardShim.release_session();
            }
            catch (BrainFlowError e)
            {
                Debug.Log(e);
            }
            Debug.Log("Brainflow streaming was stopped");
        }
        if (_model != null)
        {
            try
            {
                _model.release();
            }
            catch (BrainFlowError e)
            {
                Debug.Log(e);
            }
            Debug.Log("Brainflow ML model was released");
        }
    }

    IEnumerator Wait()
    {
        yield return new WaitForSeconds(1);
    }
}
