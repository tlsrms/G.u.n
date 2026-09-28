using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gun.RoomRhythm
{
    public sealed class StageSelectionReturn : MonoBehaviour
    {
        private bool returning;
        private void Update()
        {
            if (returning) return;
            bool pressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            if (!pressed || !Application.CanStreamedLevelBeLoaded(StageSelection.ReturnScene)) return;
            returning = true;
            SceneManager.LoadSceneAsync(StageSelection.ReturnScene);
        }
    }
}
