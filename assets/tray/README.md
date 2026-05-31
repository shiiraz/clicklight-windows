# Tray Icon Assets

These assets were cleaned from the provided transparent tray glyph by removing the neon-green matte/fringe pixels, cropping the visible glyph, centering it in a square canvas, and exporting tray-friendly sizes.

- `cursorcue-tray-clean-master.png`: cleaned source-size master.
- `cursorcue-tray.ico`: multi-size Windows icon containing `256`, `128`, `64`, `48`, `32`, `24`, `20`, and `16` PNG entries.
- `cursorcue-tray-*.png`: individual PNG exports for visual checks and packaging.

The app loads `assets/tray/cursorcue-tray.ico` at runtime and falls back to the generated code icon if the file is missing.
