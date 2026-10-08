using UnityEngine;

namespace Sid2026.Matchmaking
{
    /// <summary>
    /// Punto de entrada de la aplicación. La escena solo necesita este componente.
    /// </summary>
    public sealed class MatchmakingApp : MonoBehaviour
    {
        [Header("Identificación visible de la entrega")]
        [SerializeField] private string ownerFullName = "Simón Burgos";

        [Header("Firebase")]
        [SerializeField] private string databaseUrl =
            "https://sistemasfisicosdistribui-c957b-default-rtdb.firebaseio.com/";

        private FirebaseMatchmakingService firebaseService;
        private MatchmakingUI matchmakingUi;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            firebaseService = gameObject.AddComponent<FirebaseMatchmakingService>();
            matchmakingUi = gameObject.AddComponent<MatchmakingUI>();
            matchmakingUi.Build(ownerFullName);
            matchmakingUi.Bind(firebaseService);
        }

        private void Start()
        {
            firebaseService.Initialize(databaseUrl);
        }
    }
}
