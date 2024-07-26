using BattlePhaze.SettingsManager;
using UnityEngine;
public class SMModuleControllerSettings : SettingsManagerOption
{
    public static float JoyStickDeadZone = 0.01f;
    public override void ReceiveOption(SettingsMenuInput Option, SettingsManager Manager)
    {
        if (NameReturn(0, Option))
        {
            if (SliderReadOption(Option, Manager, out JoyStickDeadZone))
            {
                Debug.Log("JoyStick deadspace is set to " + JoyStickDeadZone);
            }
        }
    }
}