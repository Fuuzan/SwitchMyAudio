# Headphone detection

Auto-switch headphones selects a newly available headphone output when Windows reports a connection. It uses Windows' headphone/headset metadata first. A device's editable name, Bluetooth connection or USB connection alone is not enough: ordinary speakers use those connections too.

Windows may select headphones independently when this option is off. A wireless receiver that stays connected while the headset is powered off may not report a new connection when the headset wakes. A recognition rule cannot create a missing Windows event.

## Receivers reported as speakers

Some receivers expose a generic Speakers endpoint. Verified model rules supplement Windows metadata:

| Model | Shared USB identifier | Behavior |
|---|---|---|
| Audeze Maxwell receiver | `USB\VID_3329&PID_4B19` | Recognize headphone playback; prefer Game interface `MI_04` over Chat `MI_01` when both are available. |

These vendor/product/interface identifiers are shared by a hardware model. They are not serial numbers or personal endpoint IDs. Detection remains automatic; no extra device-selection setting is required.

## Report an unsupported receiver

Use the **Headphone detection** issue form on GitHub. Include the app version, Windows version, headset model, connection type and exact reconnect steps. Say whether the output disappears and returns in Windows' Sound settings, and whether clicking **Use** works.

If available, include only the shared `VID_####&PID_####` and optional `MI_##` portions from Device Manager → device Properties → Details → Hardware Ids. Do not paste a device instance path, serial number, endpoint GUID, saved settings or personal device name. You can leave the identifiers blank if unsure.

## Contributing a recognition rule

Keep verified model rules in `DeviceRules.KnownHeadphoneHardware` in `src/Model.cs`. Use an exact vendor/product boundary and an interface preference only when the interfaces are verified. Do not match generic USB/Bluetooth devices or user-editable names. Add regression checks for recognition, similar-but-different product IDs, interface ordering and ordinary speakers remaining unclassified.

Current tests cover Maxwell Game/Chat ordering, partial-ID rejection, delayed endpoints, disconnect/reconnect and manual-selection precedence. Rules for other models need evidence from that hardware; unknown identifiers are not guessed.
