using UnityEngine;
using UnityEngine.Audio;

public class SoundEqualizer : MonoBehaviour
{
    // 在 Inspector 上拖入你的 AudioMixer 資產
    public AudioMixer audioMixer;

    // 例如：設定低頻、中頻、高頻的增益 (單位 dB)
    public void SetEQ(float lowGain, float midGain, float highGain)
    {
        audioMixer.SetFloat("LowGain", lowGain);
        audioMixer.SetFloat("MidGain", midGain);
        audioMixer.SetFloat("HighGain", highGain);
    }

    // 範例：在 Start 設定一組 EQ 值
    void Start()
    {
        SetEQ(-5f, 0f, 3f); // 低頻降低5 dB，中頻不變，高頻提高3 dB
    }
}
