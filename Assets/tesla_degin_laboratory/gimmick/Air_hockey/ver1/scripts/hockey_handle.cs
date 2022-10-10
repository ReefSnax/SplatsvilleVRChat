
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;

public class hockey_handle : UdonSharpBehaviour
{
    [SerializeField] private VRCObjectSync thisobject;
    [SerializeField] private VRCObjectSync handle;

    [SerializeField] private hockey_data_memory data;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip Join;

    [SerializeField] private Ownership_change[] OwChange;

    private VRCPlayerApi player;
    private bool set = false;

    private void Update()
    {
        if(set)
        {
            data.SyncData = Networking.GetOwner(this.gameObject).displayName;

            set = false;
        }
    }

    public override void OnPickup()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, this.gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        }

        for(int j =0; j< OwChange.Length; j++)
        {
            OwChange[j].SetOwner();
        }

        set = true;

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "SE");


    }

    public override void OnDrop()
    {
        thisobject.Respawn();
        handle.Respawn();

        data.SyncData = null;
    }

    public void SE()
    {
        audioSource.PlayOneShot(Join);
    }
}
