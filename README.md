# Midi 2 vJoy

This is a simple gadget that reads a midi input emulate a joystick using vJoy, so we can use a midi controller to play games.

* Supported device: nanoKontrol2
* MIDI Cmd: CC (0xB0 cc vv)

To support other devices, edit `void MidiDevice::InitDeviceDetails()`
