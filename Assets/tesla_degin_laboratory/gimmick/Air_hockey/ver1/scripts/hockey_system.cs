
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.SDK3.Components;

public class hockey_system : UdonSharpBehaviour
{
    [UdonSynced]
    private int point_l = 0;
    [UdonSynced]
    private int point_r = 0;

    private bool disp_point = false;
    private int randomSeed;

    [UdonSynced]
    public int jk = 0;

    public bool gamemode = false;

    [SerializeField] private Text mainPoint_L;
    [SerializeField] private Text mainPoint_R;
    [SerializeField] private Text abovePoint_L1;
    [SerializeField] private Text abovePoint_L2;
    [SerializeField] private Text abovePoint_R1;
    [SerializeField] private Text abovePoint_R2;

    [SerializeField]
    private float Set_minutes = 3f;

    public bool isTimerActive = false;
    public int timetrig = 0;

    float totalTime = 0f;
    float oldSec = 0f;

    int minutes;
    float seconds;

    [SerializeField] private Text mainTimer;
    [SerializeField] private Text subTimer_L;
    [SerializeField] private Text subTimer_R;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip Start_hone;
    [SerializeField] private AudioClip End_sound;
    [SerializeField] private AudioClip Point;
    [SerializeField] private AudioClip Resp;

    [SerializeField] private hockey_trigger hockey_koma_system;

    void Update()
    {

        Disp_scores();

        if (!isTimerActive) return;

        totalTime = minutes * 60 + seconds;

        if (totalTime > Set_minutes * 60)
        {
            totalTime = Set_minutes * 60;
        }

        totalTime -= Time.deltaTime;

        minutes = (int)totalTime / 60;
        seconds = totalTime - minutes * 60;

        if (seconds <= 0)
        {
            seconds = 0f;
        }

        if ((int)seconds != (int)oldSec)
        {
            mainTimer.text = minutes.ToString("00") + ":" + ((int)seconds).ToString("00");
            subTimer_L.text = (minutes * 60 + (seconds-1)).ToString("000");
            subTimer_R.text = (minutes * 60 + (seconds-1)).ToString("000");
        }
        oldSec = seconds;

        if(totalTime <= 0)
        {
            audioSource.PlayOneShot(End_sound);
            isTimerActive = false;
            gamemode = false;
        }
    }

    public void ButtonWhich(int buttonNumber)
    {
        if(buttonNumber == 1)
        {
            //すべてのプレイヤに実行させる（同期させる）命令
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Start_hockey");
        }
        else
        {
            //すべてのプレイヤに実行させる（同期させる）命令
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Reset_hockey");
        }
    }

    public void Start_hockey()
    {
        isTimerActive = true;
        seconds = 0f;

        gamemode = true;

        minutes = (int)Set_minutes;

        mainTimer.text = Set_minutes.ToString("00") + ":" + ((int)seconds).ToString("00");
        subTimer_L.text = (Set_minutes * 60).ToString("000");
        subTimer_R.text = (Set_minutes * 60).ToString("000");

        koma_Start();
    }

    public void Reset_hockey()
    {
        mainTimer.text = Set_minutes.ToString("00") + ":00";
        subTimer_L.text = (Set_minutes * 60).ToString("000");
        subTimer_R.text = (Set_minutes * 60).ToString("000");

        isTimerActive = false;
        seconds = 0f;

        gamemode = false;

        hockey_koma_system.Reset_All();

        if (Networking.IsOwner(this.gameObject))
        {
            point_l = 0;
            point_r = 0;
        }

    }

    private void Disp_scores()
    {
        mainPoint_L.text = point_l.ToString("00");
        mainPoint_R.text = point_r.ToString("00");
        abovePoint_L1.text = point_l.ToString("00");
        abovePoint_L2.text = point_l.ToString("00");
        abovePoint_R1.text = point_r.ToString("00");
        abovePoint_R2.text = point_r.ToString("00");
}

    public void ScoreWhich(int ScoreNumber)
    {
        if (gamemode)
        {
            if (ScoreNumber == 1)
            {
                audioSource.PlayOneShot(Point);

                if (Networking.IsOwner(this.gameObject))
                {
                    point_l++;
                }

                jk = 0;

                SendCustomEventDelayedSeconds("Koma_Reset", 2.0f, VRC.Udon.Common.Enums.EventTiming.Update);
            }
            else
            {
                audioSource.PlayOneShot(Point);

                if (Networking.IsOwner(this.gameObject))
                {
                    point_r++;
                }

                jk = 1;

                SendCustomEventDelayedSeconds("Koma_Reset", 2.0f, VRC.Udon.Common.Enums.EventTiming.Update);
            }
        }
    }

    public void Koma_Reset()
    {

        audioSource.PlayOneShot(Resp);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Set_koma");
    }

    public void koma_Start()
    {
        audioSource.PlayOneShot(Start_hone);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Set_koma");
    }

    public void Set_koma()
    {
        hockey_koma_system.Reset_All();

        hockey_koma_system.Reset_Start(jk);
    }
}
