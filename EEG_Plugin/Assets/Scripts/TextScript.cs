using TMPro;
using System;
using UnityEngine;

public class TextScript : MonoBehaviour
{
    public TMP_Text canvasText;
    public Connection connection;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        canvasText.text = "Restfulness score: " + connection.prediction;
    }
    void doExitGame()
    {
        Debug.Log("Exiting game...");
        Destroy(connection);
        Application.Quit();
    }
}
