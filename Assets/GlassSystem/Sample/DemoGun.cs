using GlassSystem.Scripts;
using System.Collections;
using Arslan.Scripting;
using UnityEngine;

namespace GlassSystem.Sample
{
    public class DemoGun : MonoBehaviour
    {
        public float impactForce = 1000f;
        public int Retry = 3;


        private bool CanShoot = true;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Confined;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
               
               Invoke(nameof(Shoot),0.15f);
            }
        }

        void Shoot()
        {
            if (!CanShoot) return;
            RaycastHit hit;
           // if()
            if (GamePanelHandling.Instance.IsPaused() || GamePanelHandling.Instance.IsLevelEnded()) return;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

        public void SetAction(bool canShoot) => StartCoroutine(ActiveDeplay(canShoot ? 0.5f : 0 , canShoot));

    }
}
