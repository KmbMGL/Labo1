using UnityEngine;

namespace Workshop
{
    /// <summary>Орбитальная камера с плавными переходами, пресетами и режимом слежения за тележкой.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform followTarget;      // тележка (для пресета 5)

        Vector3 tGoal = new Vector3(0f, 1.2f, 0f), tCur = new Vector3(0f, 1.2f, 0f);
        float yawG = 0f, pitchG = 30f, distG = 24f;
        float yawC = 0f, pitchC = 30f, distC = 24f;
        bool following;

        public static readonly string[] PresetNames =
            { "1 Общий вид", "2 Станок", "3 Робот R1", "4 Робот R2", "5 Тележка", "6 Сверху" };

        void Start()
        {
            tCur = tGoal; yawC = yawG; pitchC = pitchG; distC = distG;
            Apply();
        }

        public void Preset(int i)
        {
            following = false;
            switch (i)
            {
                case 0: tGoal = new Vector3(0f, 1.2f, 0f);   yawG = 0f;   pitchG = 30f; distG = 24f; break;
                case 1: tGoal = new Vector3(0f, 1.5f, 0f);   yawG = 20f;  pitchG = 20f; distG = 8f;  break;
                case 2: tGoal = new Vector3(-4.5f, 1.3f, 0f); yawG = -25f; pitchG = 25f; distG = 10f; break;
                case 3: tGoal = new Vector3(4.5f, 1.3f, 0f);  yawG = 25f;  pitchG = 25f; distG = 10f; break;
                case 4: following = true; yawG = -35f; pitchG = 28f; distG = 9f; break;
                case 5: tGoal = new Vector3(0f, 0f, -1f);    yawG = 0f;   pitchG = 85f; distG = 32f; break;
            }
        }

        public void Orbit(float dx, float dy)
        {
            yawG += dx * 0.3f;
            pitchG = Mathf.Clamp(pitchG + dy * 0.3f, 4f, 88f);
        }

        public void Zoom(float scroll)
        {
            distG = Mathf.Clamp(distG * (1f + scroll * 0.04f), 3f, 48f);
        }

        public void Pan(float right, float forward)
        {
            following = false;
            Quaternion q = Quaternion.Euler(0f, yawG, 0f);
            tGoal += q * new Vector3(right, 0f, forward);
        }

        void LateUpdate()
        {
            if (following && followTarget != null) tGoal = followTarget.position + Vector3.up * 0.6f;
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            tCur = Vector3.Lerp(tCur, tGoal, k);
            yawC = Mathf.LerpAngle(yawC, yawG, k);
            pitchC = Mathf.Lerp(pitchC, pitchG, k);
            distC = Mathf.Lerp(distC, distG, k);
            Apply();
        }

        void Apply()
        {
            Quaternion q = Quaternion.Euler(pitchC, yawC, 0f);
            transform.position = tCur + q * Vector3.back * distC;
            transform.rotation = q;
        }
    }
}
