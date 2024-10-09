using vJoyInterfaceWrap;

namespace Midi2vJoy
{
    internal class vJoyDevice
    {
        public vJoy joystick;
        uint id = 1;
        
        public int maxButtons = 32;
        public int maxAxis = 0;
        
        List<int> axisMaxScaleFactor = [];  // if 0, axis is disabled

        public vJoyDevice(uint id)
        {
            if (id > 16)
            {
                throw new System.ArgumentException("id must be between 1 and 16");
            }

            joystick = new vJoy();
            this.id = id;

            if (!joystick.vJoyEnabled())
            {
                throw new System.ArgumentException("vJoy driver not enabled: Failed Getting vJoy attributes");
            }
            // Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString());
            VjdStat status = joystick.GetVJDStatus(id);
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                throw new System.ArgumentException($"Failed to acquire vJoy device number {id}");
            }
            // check axises
            for (uint i = 0; i < 8; i++)
            {
                HID_USAGES axis = (HID_USAGES)((int)HID_USAGES.HID_USAGE_X + i);
                if (!joystick.GetVJDAxisExist(id, axis))
                {
                    axisMaxScaleFactor.Add(0);
                    continue;
                }
                long maxval = 0x7fff;
                if (joystick.GetVJDAxisMax(id, axis, ref maxval)) {
                    axisMaxScaleFactor.Add((int)(maxval + 1) / 128);
                    maxAxis ++;
                } else {
                    axisMaxScaleFactor.Add(0);
                }
            }
            maxButtons = joystick.GetVJDButtonNumber(id);
            if (maxButtons < 8)
            {
                throw new System.ArgumentException($"vJoy device number {id} does not support 8 buttons.\n");
            }
            joystick.ResetVJD(id);
            Console.WriteLine("vJoy device number {0} acquired: buttons/axis: {1}/{2}", id, maxButtons, maxAxis);
        }

        ~vJoyDevice()
        {
            joystick.ResetVJD(id);
            joystick.RelinquishVJD(id);
        }

        public void SetAxis(int axis_id, int value)
        {
            // get N-th non-zero axis
            int axis_cnt = 0;
            int real_axis = 0;
            for (int i = 0; i < maxAxis; i++)
            {
                if (axisMaxScaleFactor[i] == 0)
                {
                    continue;
                }
                if (axis_cnt == axis_id)
                {
                    real_axis = i;
                    break;
                }
                axis_cnt ++;
            }
            joystick.SetAxis(value * axisMaxScaleFactor[real_axis], id, (HID_USAGES)((int)HID_USAGES.HID_USAGE_X + real_axis));
        }

        public void SetButton(int button_id, int btn_value)
        {
            joystick.SetBtn(btn_value >= 0x40, id, (uint)button_id + 1);    // vjoy buttons are 1-based
        }
    }
}
