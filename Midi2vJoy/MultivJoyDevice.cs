namespace Midi2vJoy
{
    internal class MultivJoyDevice
    {
        public List<vJoyDevice> vjds;

        public MultivJoyDevice(int btns, int axises)
        {
            vjds = new List<vJoyDevice>();
            int sum_btns = 0;
            int sum_axises = 0;
            for (uint i = 1; i <= 16; i++)
            {
                try {
                    var vjd = new vJoyDevice(i);
                    vjds.Add(vjd);
                    sum_btns += vjd.maxButtons;
                    sum_axises += vjd.maxAxis;
                    if (sum_btns >= btns && sum_axises >= axises)
                    {
                        break;
                    }
                } catch (Exception) {
                    continue;
                }
            }
            Console.WriteLine("Using {0} vJoy devices, total buttons/axis: {1}/{2}",
                vjds.Count, sum_btns, sum_axises);
        }

        public void SetButton(int button_id, int btn_value)
        {
            foreach (var vjd in vjds)
            {
                if (button_id < vjd.maxButtons)
                {
                    vjd.SetButton(button_id, btn_value);
                    break;
                }
                button_id -= vjd.maxButtons;
            }
        }

        public void SetAxis(int axis_id, int value)
        {
            foreach (var vjd in vjds)
            {
                if (axis_id < vjd.maxAxis)
                {
                    vjd.SetAxis(axis_id, value);
                    break;
                }
                axis_id -= vjd.maxAxis;
            }
        }
    }
}
