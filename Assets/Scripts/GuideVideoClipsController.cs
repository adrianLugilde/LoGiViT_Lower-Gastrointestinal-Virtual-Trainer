using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Video;

public class GuideVideoClipsController : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public LocalizeStringEvent localizeStringEvent;

    public VideoClip sidewaysMovementVC;
    public VideoClip rotationVC;
    public VideoClip zoomVC;
    public VideoClip panoramicMovementVC;

    private LocalizedString sidewaysMovementLS = new LocalizedString("TrainingEditorGuideTable", "sidewaysMovementGuideText");
    private LocalizedString rotationLS = new LocalizedString("TrainingEditorGuideTable", "rotationGuideText");
    private LocalizedString zoomLS = new LocalizedString("TrainingEditorGuideTable", "zoomGuideText");
    private LocalizedString panoramicMovementLS = new LocalizedString("TrainingEditorGuideTable", "panoramicMovementGuideText");

    public void SetVideoPlayerClip(int guideVideoClipIdx)
    {
        switch ((GuideVideoClips) guideVideoClipIdx)
        {
            case GuideVideoClips.SidewaysMovement:
                videoPlayer.clip = sidewaysMovementVC;
                localizeStringEvent.StringReference = sidewaysMovementLS;
                break;
            case GuideVideoClips.Rotation:
                videoPlayer.clip = rotationVC;
                localizeStringEvent.StringReference = rotationLS;
                break;
            case GuideVideoClips.Zoom:
                videoPlayer.clip = zoomVC;
                localizeStringEvent.StringReference = zoomLS;
                break;
            case GuideVideoClips.PanoramicMovement:
                videoPlayer.clip = panoramicMovementVC;
                localizeStringEvent.StringReference = panoramicMovementLS;
                break;
            default:
                videoPlayer.clip = sidewaysMovementVC;
                localizeStringEvent.StringReference = sidewaysMovementLS;
                break;
        }
    }

    public enum GuideVideoClips
    {
        SidewaysMovement = 0,
        Rotation = 1,
        Zoom = 2,
        PanoramicMovement = 3,
    }
}
