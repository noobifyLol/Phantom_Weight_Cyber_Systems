using UnityEngine;

public class PokeLogger : MonoBehaviour
{
    [SerializeField] private string messageToPrint = "Button Poked!";

    // This method will be called when the Meta SDK fires the poke click event
    public void LogPoke()
    {
        Debug.Log(messageToPrint);

        // Find the canvas by name and disable it
        GameObject disclaimer = GameObject.Find("DisclaimerCanvas");

        if (disclaimer != null)
        {
            disclaimer.SetActive(false);
        }
        else
        {
            Debug.LogWarning("DisclaimerCanvas not found in the scene.");
        }
    }
}
