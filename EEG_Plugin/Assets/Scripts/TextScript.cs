using TMPro;
using brainflow;
using UnityEngine;
using Plugins.Restfulness;

public class TextScript : MonoBehaviour
{
    public TMP_Text canvasText;
    public BoardIds boardId;

    private Predictor _predictor;

    
    // Start is called before the first frame update
    void Start()
    {
        _predictor = new Predictor(boardId);
        _predictor.OnRestfulnessScoreUpdated += OnRestfulnessScoreUpdated;
        _predictor.StartSession();
    }


    public void DoExitGame()
    {
        _predictor.StopSession();
        Debug.Log("Session released");
        // Application.Quit();
    }

    private void OnRestfulnessScoreUpdated(double score)
    {
        // TODO: TÄMÄ PASKA EI PÄIVITÄ TEKSTIÄ RUUDULLE VAIKKA ARVO PÄIVITTYY
        Debug.Log("Rest score: " + score);
        Debug.Log("What does the canvas say? (Hopefully not 'yip'): " + canvasText.text);
        canvasText.text = "Restfulness score: " + score;
    }
}
