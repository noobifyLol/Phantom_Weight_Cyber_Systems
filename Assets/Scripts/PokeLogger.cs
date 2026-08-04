using UnityEngine;

public class PokeLogger : MonoBehaviour
{
    [SerializeField] private string messageToPrint = "Button Poked!";

    // This method will be called when the Meta SDK fires the poke click event
    public void LogPoke()
    {
        Debug.Log(messageToPrint);
    }
}
