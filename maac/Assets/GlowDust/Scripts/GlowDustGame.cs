using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GlowDust
{
    /// <summary>Game flow: start, falling into a pit = game over, goal, restart (R).</summary>
    public class GlowDustGame : MonoBehaviour
    {
        public Transform player;
        public PowderThrower thrower;
        public GlowDustHUD hud;
        [Tooltip("Falling below this height is game over")]
        public float killY = -3f;

        float startTime;
        bool cleared, gameOver;

        public bool Cleared => cleared;
        public bool Finished => cleared || gameOver;

        void Start()
        {
            if (hud != null) hud.Bind(thrower);
            startTime = Time.time;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                    ? SceneManager.GetActiveScene().buildIndex : 0);
                return;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (!Finished && player != null && player.position.y < killY) GameOver();
        }

        void GameOver()
        {
            gameOver = true;
            if (thrower != null) thrower.enabled = false;
            var fpc = player.GetComponent<StarterAssets.FirstPersonController>();
            if (fpc != null) fpc.enabled = false;
            if (hud != null) hud.ShowGameOver(Time.time - startTime);
        }

        public void OnGoalReached()
        {
            if (Finished) return;
            cleared = true;
            if (thrower != null) thrower.enabled = false;
            if (hud != null) hud.ShowClear(Time.time - startTime, thrower != null ? thrower.Count : 0);
        }
    }
}
