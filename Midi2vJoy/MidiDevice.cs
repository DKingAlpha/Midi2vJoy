
using Commons.Music.Midi;

namespace Midi2vJoy
{
    internal enum LEDPattern : UInt16
    {
        UNAVAILABLE = 0,
        UNDEFINED,
        DISABLED,
        MOMENRATY,
        TOGGLE,
        BLINK,
        BREATH,
    }
    internal struct LEDConfig 
    {
        public LEDPattern pattern;
        public UInt16 period_ms;

        public LEDConfig(LEDPattern pattern, UInt16 period_ms = 0)
        {
            this.pattern = pattern;
            this.period_ms = period_ms;
        }
    }

    internal struct MidiCCConfig
    {
        public LEDConfig led_config;
        public bool is_axis;
        public int start;
        public int end;

        public MidiCCConfig(LEDConfig led_config, bool is_axis, int start, int end)
        {
            this.led_config = led_config;
            this.is_axis = is_axis;
            this.start = start;
            this.end = end;
        }
    }

    internal class MidiDevice
    {
        private IMidiInput input;
        private IMidiOutput output;

        private MultivJoyDevice vjd;

        private MidiCCConfig[] cc_ranges = [];
        private List<byte> cmd_leds_on = [];
        private List<byte> cmd_leds_off = [];

        private bool[] status_btns = [];
        private bool[] status_btn_toggle = [];
        private int[] status_axis = [];

        public MidiDevice()
        {
            InitDeviceDetails();
            vjd = new MultivJoyDevice(status_btns.Length, status_axis.Length);

            var access = MidiAccessManager.Default;
            var iport = access.Inputs.Last();
            var oport = access.Outputs.Last();
            input = access.OpenInputAsync(iport.Id).Result;
            Console.WriteLine("Opening input midi device [{0}] {1}", iport.Id, iport.Name);
            output = access.OpenOutputAsync(oport.Id).Result;
            Console.WriteLine("Opening output midi device [{0}] {1}", oport.Id, oport.Name);

            output.Send(cmd_leds_off.ToArray(), 0, cmd_leds_off.Count, 0);
            BindEvent();
        }

        ~MidiDevice()
        {
            output.Send(cmd_leds_off.ToArray(), 0, cmd_leds_off.Count, 0);
            input.CloseAsync().Wait();
            output.CloseAsync().Wait();
        }
        
        protected string HexifyBytes(byte[] bytes)
        {
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        virtual protected void BindEvent() {
            input.MessageReceived += (obj, e) => {
				// Console.WriteLine ($"{e.Timestamp} {e.Start} {e.Length} {HexifyBytes(e.Data)}");
                if (e.Data.Length != 3) {
                    return;
                }
                // channel = 0
                if (e.Data[0] != 0xb0) {
                    return;
                }
                // CC
                int cc = e.Data[1];
                int value = e.Data[2];
                int prev_btn_id = 0;
                int prev_axis_id = 0;
                foreach (var cc_range in cc_ranges) {
                    if (cc >= cc_range.start && cc <= cc_range.end) {
                        if (cc_range.is_axis) {
                            int axis_id = prev_axis_id + cc - cc_range.start;
                            vjd.SetAxis(axis_id, value);
                            status_axis[axis_id] = value;
                            prev_axis_id += cc_range.end - cc_range.start + 1;
                        } else {
                            int btn_id = prev_btn_id + cc - cc_range.start; 
                            vjd.SetButton(btn_id, value);
                            var is_pressed = value >= 0x40;
                            status_btns[btn_id] = is_pressed;
                            var led_config = GetLedConfigOverlay(cc) ?? cc_range.led_config;
                            if (led_config.pattern == LEDPattern.MOMENRATY) {
                                output.Send([0xb0, (byte)cc, (byte)(is_pressed?127:0)], 0, 3, 0);
                            } else if (led_config.pattern == LEDPattern.TOGGLE) {
                                if (is_pressed) {
                                    status_btn_toggle[btn_id] = !status_btn_toggle[btn_id];
                                }
                                /*
                                    PRESS-RELEASE x4   ON    OFF      ON      OFF
                                    IN  - is_pressed:  1 0   1 0      1 0     1 0
                                    IN  - btn_toggle:  1 1   0 0      1 1     0 0
                                    OUT - led_output:  1 1   1 0      1 1     1 0
                                */
                                if (is_pressed && status_btn_toggle[btn_id]) {
                                    output.Send([0xb0, (byte)cc, 127], 0, 3, 0);
                                }
                                else if (!is_pressed && !status_btn_toggle[btn_id]) {
                                    output.Send([0xb0, (byte)cc, 0], 0, 3, 0);
                                }
                            } else {
                                // throw new NotImplementedException();
                            }
                            prev_btn_id += cc_range.end - cc_range.start + 1;
                        }
                        break;
                    } else {
                        if (cc_range.is_axis) {
                            prev_axis_id += cc_range.end - cc_range.start + 1;
                        } else {
                            prev_btn_id += cc_range.end - cc_range.start + 1;
                        }
                    }
                }
			};
        }

        virtual protected void InitDeviceDetails()
        {
            cc_ranges = [
                // has_led, led_pattern, is_axis, cc start, cc end
                new MidiCCConfig(new LEDConfig(LEDPattern.UNAVAILABLE), true,  0,  7),  // slider
                new MidiCCConfig(new LEDConfig(LEDPattern.UNAVAILABLE), true,  16, 23), // knob
                new MidiCCConfig(new LEDConfig(LEDPattern.TOGGLE),      false, 32, 39), // button - solo
                new MidiCCConfig(new LEDConfig(LEDPattern.TOGGLE),      false, 48, 55), // button - mute
                new MidiCCConfig(new LEDConfig(LEDPattern.TOGGLE),      false, 64, 71), // button - record
                new MidiCCConfig(new LEDConfig(LEDPattern.MOMENRATY),   false, 41, 46), // transport - play, stop, rewind, ff, record, cycle
                new MidiCCConfig(new LEDConfig(LEDPattern.UNAVAILABLE), false, 58, 59), // track - rewind, track ff
                new MidiCCConfig(new LEDConfig(LEDPattern.UNAVAILABLE), false, 60, 62)  // marker - set, rewind ff
            ];
            int axis_count = 0;
            int btn_count = 0;
            foreach (var cc_range in cc_ranges) {
                if (cc_range.is_axis) {
                    axis_count += cc_range.end - cc_range.start + 1;
                } else {
                    btn_count += cc_range.end - cc_range.start + 1;
                }
                if (cc_range.led_config.pattern == LEDPattern.UNAVAILABLE) {
                    continue;
                }
                for (int cc = cc_range.start; cc <= cc_range.end; cc++) {
                    cmd_leds_on.Add(0xb0);
                    cmd_leds_on.Add((byte)cc);
                    cmd_leds_on.Add(127);

                    cmd_leds_off.Add(0xb0);
                    cmd_leds_off.Add((byte)cc);
                    cmd_leds_off.Add(0);
                }
            }
            status_btns = new bool[btn_count];
            status_btn_toggle = Enumerable.Repeat(false, btn_count).ToArray();
            status_axis = new int[axis_count];
        }

        LEDConfig? GetLedConfigOverlay(int cc)
        {
            return null;
        }

    }
}
