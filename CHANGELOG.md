# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Sample with a script to test vibrations on gamepad via Nice Vibrations
- *Shift* locks time and *Alt* locks value of dragged point in addition to selected point drag mode in the top UI

### Fixed
- Plot X axis first and labels are now in correct positions

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
