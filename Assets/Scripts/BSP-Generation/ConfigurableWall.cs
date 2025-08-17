using UnityEngine;

namespace BSP_Generation
{
    public class ConfigurableWall : MonoBehaviour
    {
        [SerializeField] private Transform Visuals;

        public void ConfigureHeight(float height)
        {
            Visuals.localScale = new Vector3(1, height, 1);
            Visuals.localPosition = new Vector3(0, height / 2, 0);
        }
    }
}