using GlassSystem.Scripts;
using System.Collections;
using Arslan.Scripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GlassSystem.Sample
{
    public class DemoGun : MonoBehaviour
    {
        public float impactForce = 1000f;
        public int Retry = 3;


        private bool CanShoot = true;
        private Coroutine _shootRoutine;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Confined;
        }

        void Update()
        {
            if (TryGetShootPosition(out Vector2 screenPosition))
            {
                if (_shootRoutine != null)
                    StopCoroutine(_shootRoutine);

                _shootRoutine = StartCoroutine(ShootAfterDelay(screenPosition));
            }
        }

        bool TryGetShootPosition(out Vector2 screenPosition)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                int touchId = touchscreen.primaryTouch.touchId.ReadValue();
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId))
                {
                    screenPosition = Vector2.zero;
                    return false;
                }

                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    screenPosition = Vector2.zero;
                    return false;
                }

                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = Vector2.zero;
            return false;
        }

        IEnumerator ShootAfterDelay(Vector2 screenPosition)
        {
            yield return new WaitForSeconds(0.15f);
            Shoot(screenPosition);
            _shootRoutine = null;
        }

        void Shoot(Vector2 screenPosition)
        {
            if (!CanShoot) return;
            RaycastHit hit;
            if (GamePanelHandling.Instance.IsPaused() || GamePanelHandling.Instance.IsLevelEnded()) return;
            var ray = Camera.main.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.yellow, 10);
                var glass = hit.collider.gameObject.GetComponent<BaseGlass>();


                if (glass is not null)
                {
                    int failBreak = 0;
                    while (true)
                        try
                        {
                            VibrationsHandler.GlassTap();
                            glass.Break(hit.point, ray.direction * impactForce);
                            CameraShake.Instance?.Shake();
                            return;
                        }
                        catch (InternalGlassException e)
                        {
                            if (++failBreak >= Retry)
                                throw;
                            Debug.LogWarning($"Failed to break glass (retry {failBreak}): {e}");
                        }
                }
            }
        }


        public IEnumerator ActiveDeplay(float time = 0, bool _canShoot = false)
        {
            yield return new WaitForSeconds(time);
            CanShoot = _canShoot;
        }

        public void SetAction(bool canShoot) => StartCoroutine(ActiveDeplay(canShoot ? 0.5f : 0, canShoot));
    }
}