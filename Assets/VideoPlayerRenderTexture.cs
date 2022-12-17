using UnityEngine;
using UnityEngine.Video;

public class VideoPlayerRenderTexture : MonoBehaviour {
  public void Start() {
    GetComponent<VideoPlayer>().targetTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
  }
}
