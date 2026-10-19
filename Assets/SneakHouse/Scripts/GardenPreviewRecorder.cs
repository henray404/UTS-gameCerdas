#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

namespace SneakHouseEditor
{
    // Temporary capture helper, never attached to the saved scene or included in a build.
    public class GardenPreviewRecorder : MonoBehaviour
    {
        public bool environmentOnly;
        private IEnumerator Start()
        {
            SneakHouse.MissionFlowUI menu=FindFirstObjectByType<SneakHouse.MissionFlowUI>();
            bool restoreMenu=menu!=null && menu.enabled;
            if(environmentOnly && menu!=null)menu.enabled=false;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp",environmentOnly?"work/garden-motion-v2-frames":"work/garden-preview-frames"));
            Directory.CreateDirectory(folder);
            int frames=environmentOnly?72:48;
            float step=environmentOnly?1f/12:.125f;
            float nextCapture=Time.realtimeSinceStartup;
            for(int i=0;i<frames;i++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,i.ToString("D3")+".png"));
                nextCapture+=step;
                yield return new WaitForSecondsRealtime(Mathf.Max(0,nextCapture-Time.realtimeSinceStartup));
            }
            if(menu!=null)menu.enabled=restoreMenu;
            Debug.Log("GARDEN_PREVIEW_CAPTURED: "+folder);
            Destroy(gameObject);
        }
    }
}
#endif
