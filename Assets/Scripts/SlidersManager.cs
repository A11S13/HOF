using UnityEngine;
using UnityEngine.UI;

public class SlidersManager : MonoBehaviour
{
    // Sliders for energy distribution
    public Slider shieldsSlider;
    public Slider enginesSlider;
    public Slider weaponsSlider;
    public Slider commandSlider;

    // Flag to prevent infinite loop during programmatic changes
    private bool isChangingByScript = false;

    void Start()
    {
        // Subscribe to slider value change events
        shieldsSlider.onValueChanged.AddListener(value => OnSliderValueChanged(value, shieldsSlider, enginesSlider, weaponsSlider, commandSlider));
        enginesSlider.onValueChanged.AddListener(value => OnSliderValueChanged(value, enginesSlider, shieldsSlider, weaponsSlider, commandSlider));
        weaponsSlider.onValueChanged.AddListener(value => OnSliderValueChanged(value, weaponsSlider, shieldsSlider, enginesSlider, commandSlider));
        commandSlider.onValueChanged.AddListener(value => OnSliderValueChanged(value, commandSlider, shieldsSlider, enginesSlider, weaponsSlider));
    }

    void Update()
    {
        // Reset flag after frame update
        if (isChangingByScript)
        {
            isChangingByScript = false;
        }
    }

    void OnSliderValueChanged(float sliderValue, Slider currentSlider, Slider otherSlider1, Slider otherSlider2, Slider otherSlider3)
    {
        if (!isChangingByScript)
        {
            // Set flag to prevent infinite loop
            isChangingByScript = true;

            // Calculate the total value excluding the current slider
            float totalValue = shieldsSlider.value + enginesSlider.value + weaponsSlider.value + commandSlider.value - sliderValue;

            if (Mathf.Approximately(totalValue, 0f))
            {
                // If totalValue is zero, evenly distribute the remaining value among all sliders
                float equalDistribution = 1f / 4f;
                SetSliderValueWithoutNotify(otherSlider1, equalDistribution);
                SetSliderValueWithoutNotify(otherSlider2, equalDistribution);
                SetSliderValueWithoutNotify(otherSlider3, equalDistribution);
                SetSliderValueWithoutNotify(currentSlider, 0f); // Set the current slider to 0
            }
            else
            {
                // Calculate the remaining value
                float remainingValue = 1.0f - sliderValue;

                // Calculate the target values for other sliders based on the changes in the current slider
                float targetValue1 = remainingValue * (otherSlider1.value / totalValue);
                float targetValue2 = remainingValue * (otherSlider2.value / totalValue);
                float targetValue3 = remainingValue * (otherSlider3.value / totalValue);

                // Update the other sliders without triggering their events
                SetSliderValueWithoutNotify(otherSlider1, targetValue1);
                SetSliderValueWithoutNotify(otherSlider2, targetValue2);
                SetSliderValueWithoutNotify(otherSlider3, targetValue3);

                // Ensure the sliders won't go below 0 or above 1
                SetSliderValueWithoutNotify(otherSlider1, Mathf.Clamp(targetValue1, 0.01f, 0.99f)); // Avoid setting to extreme values
                SetSliderValueWithoutNotify(otherSlider2, Mathf.Clamp(targetValue2, 0.01f, 0.99f));
                SetSliderValueWithoutNotify(otherSlider3, Mathf.Clamp(targetValue3, 0.01f, 0.99f));

                // Calculate the updated value for the current slider to ensure the resulting sum is 1
                SetSliderValueWithoutNotify(currentSlider, 1f - otherSlider1.value - otherSlider2.value - otherSlider3.value);

                // Ensure the dragged slider doesn't stop sliding at its max value
                if (sliderValue >= 1f)
                {
                    SetSliderValueWithoutNotify(currentSlider, 1f);
                }
            }          
        }
    }

    // Helper method to update slider value without triggering its event
    private void SetSliderValueWithoutNotify(Slider slider, float value)
    {
        slider.value = value;
    }
}
