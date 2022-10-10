
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;

public class hockey_trigger : UdonSharpBehaviour
{
    [SerializeField] private hockey_system system;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip collisionSE;
    [SerializeField] private AudioClip chargeSE;
    [SerializeField] private AudioClip speadUpSE;

    [SerializeField] private GameObject trig_R;
    [SerializeField] private GameObject trig_L;

    private const float FIXED_TIME_STEP = 0.00225f;

    private Vector3 Posi = new Vector3(0, 0, 0);
    private Vector3 Velocity = new Vector3(0, 0, 0);
    private Vector3 In_posi = new Vector3(0, 0, 0);

    [UdonSynced]
    private Vector3 currentPosition = new Vector3(0, 0, 0);
    [UdonSynced]
    private Vector3 currentVelocity = new Vector3(0, 0, 0);

    [UdonSynced]
    private bool otherchange = false;
    private float tmptime = 0f;

    private bool ChangeValue = false;

    [SerializeField] private ParticleSystem m_Particle;

    void Start()
    {
        In_posi = this.gameObject.transform.localPosition;
    }
    void Update()
    {
        //同期位置と速度がセットされたら(マレットが陣地に触れたら)(グローバル判断)
        if ((otherchange) && (!ChangeValue))
        {
            if (!Networking.IsOwner(this.gameObject))
            {
                this.gameObject.transform.localPosition = currentPosition;
                Velocity = currentVelocity;

                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Allow_SyncvalSet");
            }
            else
            {
                this.gameObject.transform.localPosition += (Velocity * FIXED_TIME_STEP);
                Posi = this.gameObject.transform.localPosition;
            }
        }
        //駒がナットに触れたら(ローカル判断)
        else if (ChangeValue)
        {

            this.gameObject.transform.localPosition = currentPosition;
            Velocity = currentVelocity;

            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Allow_SyncvalSet2");

            ChangeValue = false;
        }
        //通常時
        else
        {
            this.gameObject.transform.localPosition += (Velocity * FIXED_TIME_STEP);
            Posi = this.gameObject.transform.localPosition;
        }

        //速度の丸め処理
        if (Velocity.x > 1.8f)
        {
            Velocity.x = 1.8f;
        }
        else if (Velocity.x < -1.8f)
        {
            Velocity.x = -1.8f;
        }
        if (Velocity.y > 1.8f)
        {
            Velocity.y = 1.8f;
        }
        else if (Velocity.y < -1.8f)
        {
            Velocity.y = -1.8f;
        }

        //ビリヤードの台の範囲内への丸め処理
        if ((-0.1648f > Posi.x) || (Posi.x > 0.1648f))
        {
            if ((Posi.y <= -0.0196f) || (0.0196f <= Posi.y))
            {
                audioSource.PlayOneShot(collisionSE);
                Velocity.x = -Velocity.x;

                /*
                if (!Networking.IsOwner(this.gameObject))
                {
                    if (yobi_num == 1)
                    {
                        ChangeValue = true;

                        yobi_num = 2;
                    }
                }
                */
            }
        }
        if ((-0.0849f > Posi.y) || (Posi.y > 0.0849f))
        {
            audioSource.PlayOneShot(collisionSE);
            Velocity.y = -Velocity.y;

            /*
            if (!Networking.IsOwner(this.gameObject))
            {
                if (yobi_num == 1)
                {
                    ChangeValue = true;

                    yobi_num = 2;
                }
            }
            */
        }
    }

    public void Allow_SyncvalSet()
    {
        if (Networking.IsOwner(this.gameObject))
        {
            otherchange = false;
        }

    }


    public void Allow_SyncvalSet2()
    {
        if (!Networking.IsOwner(this.gameObject))
        {
            this.gameObject.transform.localPosition = currentPosition;
            Velocity = currentVelocity;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        //ゲートに触れたら
        if (other.gameObject.name == "Switch_trig_R")
        {
            //駒のオーナーである場合
            if (Networking.IsOwner(this.gameObject))
            {
                //trig_R(Rゲートのオーナでない場合
                if (!Networking.IsOwner(this.trig_R))
                {

                    Velocity = Vector3.zero;

                    m_Particle.Play();
                    audioSource.PlayOneShot(chargeSE);
                }

            }
            //駒のオーナでない場合
            else
            {
                //trig_R(ゲート)のオーナの場合
                if (Networking.IsOwner(this.trig_R))
                {
                    Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

                    currentVelocity = Velocity * 2.0f;
                    currentPosition = this.gameObject.transform.localPosition;

                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Sync_value");
                }
            }
        }
        else if (other.gameObject.name == "Switch_trig_L")
        {
            if (Networking.IsOwner(this.gameObject))
            {
                if (!Networking.IsOwner(this.trig_L))
                {
                    Velocity = Vector3.zero;

                    m_Particle.Play();
                    audioSource.PlayOneShot(chargeSE);
                }

            }
            else
            {

                if (Networking.IsOwner(this.trig_L))
                {
                    Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

                    currentVelocity = Velocity * 2.0f;
                    currentPosition = this.gameObject.transform.localPosition;

                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Sync_value");
                }
            }
        }
        //ゴールに触れたら
        else if ((other.gameObject.name == "trigger_R") && (Networking.IsOwner(this.trig_R)))
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Score1");
        }
        else if ((other.gameObject.name == "trigger_L") && (Networking.IsOwner(this.trig_L)))
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Score2");
        }

    }

    public void Sync_value()
    {
        if (Networking.IsOwner(this.gameObject))
        {
            otherchange = true;
        }
        else
        {
            audioSource.PlayOneShot(speadUpSE);
        }
    }

    public void Score1()
    {
        Velocity = Vector3.zero;
        system.ScoreWhich(1);
    }

    public void Score2()
    {
        Velocity = Vector3.zero;
        system.ScoreWhich(2);
    }

    void OnCollisionEnter(Collision collision)
    {
        if ((collision.gameObject.name == "handle001") || (collision.gameObject.name == "handle002"))
        {

            var val = Velocity;

            val.x = -val.x;

            var sinA = val.y / Mathf.Sqrt(Mathf.Pow(val.x, 2.0f) + Mathf.Pow(val.y, 2.0f));
            var cosA = val.x / Mathf.Sqrt(Mathf.Pow(val.x, 2.0f) + Mathf.Pow(val.y, 2.0f));

            val.x = val.x * cosA - val.y * sinA;
            val.y = val.x * sinA + val.y * cosA;

            Velocity = val * 1.2f;

            if (Networking.IsOwner(this.gameObject))
            {
                currentPosition = this.gameObject.transform.localPosition;

                currentVelocity = Velocity;

            }

            ChangeValue = true;

            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Bounce_koma");
        }
        else
        {

            /*
            var val = Velocity;

            var sinA = val.y / Mathf.Sqrt(Mathf.Pow(val.x, 2.0f) + Mathf.Pow(val.y, 2.0f));
            var cosA = val.x / Mathf.Sqrt(Mathf.Pow(val.x, 2.0f) + Mathf.Pow(val.y, 2.0f));

            val.x = val.x * cosA - val.y * sinA;
            val.y = val.x * sinA + val.y * cosA;

            Velocity = val * 1.2f;

            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "Bounce_koma");
            */
        }
    }

    public void Bounce_koma()
    {
        audioSource.PlayOneShot(collisionSE);

        //観戦者のための同期指示
        if ((!Networking.IsOwner(this.trig_L))||(!Networking.IsOwner(this.trig_R)))
        {
            ChangeValue = true;
        }

    }

    public void Reset_Start(int j)
    {
        var val2 = Velocity;

        if (j == 0)
        {
            val2.x = 0.4f;
            val2.y = 0.4f;
        }
        else
        {
            val2.x = -0.4f;
            val2.y = -0.4f;
        }

        Velocity = val2;

    }

    public void Reset_All()
    {
        this.gameObject.transform.localPosition = In_posi;
        Velocity = Vector3.zero;

    }

}
