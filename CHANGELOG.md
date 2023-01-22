# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2023-01-22
### Added
- Basic algorithms to automatically generate haptics:
	- Transient events with onsets detection (base on [this article](https://medium.com/giant-scam/algorithmic-beat-mapping-in-unity-preprocessed-audio-analysis-d41c339c135a))
	- Continuous envelopes generations from RMS and FFT
- Window supports *.haptic* files now for both import and export
- Import and export options have been added:
	- Points can now be processed while importing or exporting:
		- linear (default)
		- power of 2
		- power of 2.28 ([more info](https://danielbuettner.medium.com/10-things-you-should-know-about-designing-for-apple-core-haptics-9219fdebdcaa))
	- While saving you can select output format - AHAP or Haptic, additionaly use *.json* extension
- Safe Mode - shows warning dialog popups before critical operations, can be toggled in window's context menu
- *Stop* button for audio clip

### Changed
- Zoom now respects mouse position
- Tweaked UI

### Fixed
- Ending mouse drag outside plot while selecting points won't break the window
- Point drag bounds won't display when only transient points are being moved
- Point drag bounds are now properly calculated when using advanced panel


## [0.4.1] - 2022-12-08
### Changed
- AHAP editor window will now auto-dock next to scene view

### Fixed
- Changed package assembly definition to be only included in editor, so it won't try to compile into builds (and miserably fail)


## [0.4.0] - 2022-12-04
### Added
- Point selection - switch mode in top bar or hold *Ctrl* to select multiple points
- You can now drag multiple selected points, limits are visualised with yellow dotted lines, middle mouse button selects whole event if hovering over a point
- *Point editing* section in top bar UI contains *Advanced panel* toggle which enables resizable side panel with additional options and information:
	- Selected point info (shows parameters of single selected point)
	- Point drag mode (lock time or value - doubles holding *Shift* or *Alt* key
	- Mouse snapping
	- Hover info (missing in previous version)
- Time can now be locked during continuous event creation
- Draw culling - points that are not invisible in scroll view are not being drawn
- Window context menu contains debug mode switch and *Reset* button that should fix potential issues without the need to reopen the window
- When trying to use an audio file longer than 30 seconds (*MAX_TIME*) a warning dialog is displayed

### Changed
- Reference audio texture is now showing only positive amplitude to better reflect how the vibration parameters should be setup
- Swapped *Shift* and *Alt* key to lock time and value accordingly
- Re-writted mouse handling so different functionalities can be used in various mouse modes
- Split code into more files

### Fixed
- Guide lines are now properly following selected snapping setting
- Waveform texture is now scaling correctly


## [0.3.0] - 2022-09-18
### Added
- Sample with a script to test vibrations on gamepad via Nice Vibrations
- *Shift* locks time and *Alt* locks value of dragged point in addition to selected point drag mode in the top UI
- Creating continuous events now remembers mouse drag start value
- Audio clip preview button

### Changed
- Redesigned GUI and reworked drawing code (e.g. separated input handling from drawing)
- Dragged points are now highlighted differently

### Fixed
- Plot X axis first and labels are now in correct positions
- Audio waveform is now drawn under horizontal grid lines


## [0.2.2] - 2022-08-21
### Added
- Waveform scale parameter
- Project name field

### Changed
- Organized code
  - Moved plot event help classes to separate file
  - Added global parameters on top of the file for easier settings adjustments


## [0.2.1] - 2022-07-23
### Fixed
- Importing file with a single point curve where point overlaps with last point of previous curve won't result in infinite loop/memory leak
- Exporting events with curves with multiples of 16 points won't create unnecessary parameter curve with a single point
- Removed unwanted logs used for hover detection fix in version 0.2.0


## [0.2.0] - 2022-07-19
### Added
- Reference audio clip texture on plot

### Fixed
- Hover detection for transient event


## [0.1.1] - 2022-07-18
### Changed
- Added more vertical guide lines on plots
- Minor code optimizations


## [0.1.0] - 2022-07-17
### Initial release
