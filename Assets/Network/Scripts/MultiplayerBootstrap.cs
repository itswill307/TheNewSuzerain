using UnityEngine;
using TMPro;                   // or TMPro
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class MultiplayerBootstrap : MonoBehaviour
{
    [SerializeField] TMP_InputField joinCodeInput;  // or TMP_InputField
    [SerializeField] TMP_Text joinCodeLabel;        // or TMP_Text

    // Called by the Host button
    public async void Host()
    {
        await SessionInit.EnsureReady();

        // Create a Sessions "hosted" game using Unity Relay under the hood
        var options = new SessionOptions { MaxPlayers = 2 }
            .WithRelayNetwork(); // or .WithDistributedAuthorityNetwork()

        var session = await MultiplayerService.Instance.CreateSessionAsync(options);
        if (joinCodeLabel) joinCodeLabel.text = "Join Code: " + session.Code;

        // (Default behavior) Sessions integrates with NGO and brings clients in.
        // Now load the game scene as the host; clients will follow via NGO scene sync
        NetworkManager.Singleton.SceneManager.LoadScene("Map", LoadSceneMode.Single);
    }

    // Called by the Join button
    public async void Join()
    {
        await SessionInit.EnsureReady();
        var code = joinCodeInput.text.Trim().ToUpperInvariant();

        await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
        // Client auto-connects & will follow the host’s scene via NGO scene sync
    }
}
