# Documentation Image Helper
A small utility to mark help write better user documentation steps - draw lines or shapes on images from clipboard and copy the final edit back to clipboard. 

# Usage
Take a screenshot, paste the image into the app, mark it, copy it and the edited image is ready for pasting.

## Available tools and the way they work

- **Paste** — Loads the current clipboard image onto the canvas (or press `Ctrl+V`). If the clipboard does not hold an image, the canvas shows "Clipboard is not an image".
- **Undo / Redo** — Step backwards or forwards through your edits (`Ctrl+Z` / `Ctrl+Y`).
- **Color** — Sets the color used by every drawing tool: Red, Green, Blue, White or Black.
- **Width** — Slider that sets the thickness of lines and shape outlines.
- **Text** — Click on the image to place text, type, then press `Ctrl+Enter` to confirm or `Esc` to cancel.
  - **Font** — The dropdown beside it sets the text size.
- **Line** — Hold the left mouse button and drag to draw freehand. Hold `Shift` while dragging to draw a straight line instead.
- **Rectangle** — Drag from one corner to the opposite corner to draw a rectangle outline.
- **Circle** — Click to set the center, then drag outwards; the drag distance becomes the radius.
- **Oval** — Drag a bounding box to draw an ellipse that fills it.
- **Steps** — Drops auto-numbered badges for step-by-step guides. Hold `Shift` and click to place 1, 2, 3, and so on; releasing `Shift` resets the count back to 1.
- **Snap Line** — Click to place points one after another, with each segment snapping to the nearest chosen angle. Double-click or press `Enter` to finish the line, or `Esc` to cancel it.
  - **Snap** — The dropdown sets the snap angle in degrees (10, 20, 45, 60, 75, 90).
- **Crop** — Drag a region to crop the image down to just that area.
- **Copy** — Copies the edited image back to the clipboard, ready to paste elsewhere (`Ctrl+C`).
- **Moving around the canvas**
  - Zoom: scroll the mouse wheel (zooms toward the cursor).
  - Pan: hold the middle mouse button and drag.

# License
SOURCE-AVAILABLE LICENSE (Non-Redistributable)

Copyright (c) 2025-2026 Rév Oconner. All Rights Reserved.
