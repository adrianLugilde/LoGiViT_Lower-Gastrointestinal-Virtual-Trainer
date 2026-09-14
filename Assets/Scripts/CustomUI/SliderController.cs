using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SliderController : MonoBehaviour
{
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI valueLabel;
    public Slider slider;

    public void SetInitialValue(float value)
    {
        slider.value = value;
    }

    public void UpdateValueLabel()
    {
        valueLabel.text = slider.value.ToString();
        OnValueChanged?.Invoke();
    }

    public UnityEvent OnValueChanged = new UnityEvent();
}
