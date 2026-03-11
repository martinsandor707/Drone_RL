using Unity.AppUI.UI;
using UnityEngine;

public class GUI_DroneAgent : MonoBehaviour
{
    [SerializeField] private DroneAgent _droneAgent;

    private GUIStyle _defaultStyle = new GUIStyle();
    private GUIStyle _positiveStyle = new GUIStyle();
    private GUIStyle _negativeStyle = new GUIStyle();


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _defaultStyle.fontSize = 60;
        _defaultStyle.normal.textColor = Color.yellow;

        _positiveStyle.fontSize = 60;
        _positiveStyle.normal.textColor = Color.green;

        _negativeStyle.fontSize = 60;
        _negativeStyle.normal.textColor = Color.red;
        
    }

    private void OnGUI()
    {
        string debugEpisode = "Episode: " + _droneAgent.CurrentEpisode + " - Step: " + _droneAgent.StepCount;
        string debugReward = "Reward: " + _droneAgent.CumulativeReward.ToString();

        // Select style based on reward
        GUIStyle rewardStyle = _droneAgent.CumulativeReward > 0 ? _positiveStyle : _negativeStyle;

        GUI.Label(new Rect(20,20,500,30), debugEpisode, _defaultStyle);
        GUI.Label(new Rect(20,80,500,30), debugReward, rewardStyle); 

    }

    // Update is called once per frame
    void Update()
    {
        
    }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void slowTime()
    {
        Time.timeScale = 0.2f;
        
    }
    public void normalSpeed()
    {
        Time.timeScale=1;
    }

}
