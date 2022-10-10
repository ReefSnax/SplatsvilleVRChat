
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class hockey_data_memory : UdonSharpBehaviour
{
    [SerializeField]
    private Text TargetText = null;

    [UdonSynced]
    public string SyncData = string.Empty;


    private void Update()
    {
        this.TargetText.text = this.SyncData;
    }
}
