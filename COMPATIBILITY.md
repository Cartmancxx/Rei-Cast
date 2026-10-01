# D-Cast compatibility

Rei Cast targets the **Windows extended-display interface**. It does not contain a cooler USB driver or a model allowlist. A D-Cast-enabled cooler is eligible when its screen appears as a non-primary Windows monitor with nonzero bounds; users can select it regardless of size or product name.

The renderer uses landscape, portrait or compact layouts with uniform scaling. Software rendering checks are not physical hardware certification. Very small panels may make text difficult to read even when a window can be drawn.

| Device / class | Official D-Cast evidence | Rei Cast validation |
| --- | --- | --- |
| LT360 VISION | D-Cast enabled on the original user's unit | Physical Windows window and task/startup behavior exercised; wake transport still needs physical confirmation |
| [LT360 VISION ARGB](https://global.deepcool.com/products/Cooling/cpuliquidcoolers/LT360-VISION-ARGB-4.5-Ultrawide-LCD-Liquid-Cooler-with-Adjustable-Viewing-Angle/2026/22717.shtml) | Official product page describes an extended system display | Interface-compatible; this variant not independently tested |
| [LT360 VISION WH ARGB](https://deepcool.com/products/Cooling/cpuliquidcoolers/LT360-VISION-WH-ARGB-4.5-Ultrawide-LCD-Liquid-Cooler-with-Adjustable-Viewing-Angle/2026/23018.shtml) | Official product page lists D-Cast | Interface-compatible; not independently tested |
| Other D-Cast-capable coolers | Confirm D-Cast support on that model and in installed DeepCreative | Select the Windows secondary display; community hardware reports needed |
| Other D-Cast screens, e.g. [CH690 LCD](https://global.deepcool.com/products/Cases/CH690-LCD-Full-View-Tempered-Glass-ATX-Case-with-LCD-Screen/2026/23193.shtml) | Official product page describes a secondary display | Same display path can be used; not physically tested |
| Digital-only / media-upload-only cooler screens | No Windows extended-display interface | Outside this application's display transport |

This list intentionally does not infer D-Cast support merely because a product has an LCD. New models require no app update when their screen is already exposed through the same Windows interface. If an automatic name is unknown, choose it manually.

## Automatic selection

1. Exclude the primary monitor.
2. Match monitor/adapter names containing D-Cast, Dcast, DeepCool, or the JZFSDisplayDriver name observed on the original hardware.
3. Select only if there is a single matching monitor.
4. If no name matches, retain a unique 854×480 or 480×854 legacy fallback.
5. Otherwise remain in the tray until a monitor is selected explicitly.

The saved monitor interface identity is obtained through Windows `EnumDisplayDevices` with `EDD_GET_DEVICE_INTERFACE_NAME`. It is a best-effort identity, not a universal promise that a driver will retain it forever. If Windows or a driver changes both identifiers, select the screen again.

## Report a model

Use the compatibility issue template. Include only model, Windows/DeepCreative versions, resolution/orientation, selection method and outcome. A crop of the screen is optional; remove any private dialogue before sharing. Do not upload full local logs or monitor interface identifiers.
