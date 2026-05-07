using GlassSystem.Scripts;
using UnityEngine;

namespace GlassSystem.Sample
{
    public class DemoGun : MonoBehaviour
    {
        public float impactForce = 1000f;
        public int Retry = 3;
        
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Confined;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                RaycastHit hit;
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
        }
    }
}
