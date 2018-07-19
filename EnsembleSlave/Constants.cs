using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EnsembleSlave
{
    public class Constants
    {
        public const double TopMargin = 90;

        public static bool BluetoothWindowIsOpen = false;
        public static string BLUETOOTH_ID = "";
        public static string RSOURCES_PATH = System.IO.Path.GetFullPath("..\\..\\..\\Resources") + "\\";

        public const int COLOR_WIDTH = 640;
        public const int COLOR_HEIGHT = 480;
        public const int COLOR_FPS = 30;

        public const int DEPTH_WIDTH = 640;
        public const int DEPTH_HEIGHT = 480;
        public const int DEPTH_FPS = 30;

        public static bool RealSenseIsConnect = false;

        public static Brush[] brushes = new Brush[]
        {
            Brushes.Red,
            Brushes.OrangeRed,
            Brushes.Orange,
            Brushes.Yellow,
            Brushes.YellowGreen,
            Brushes.Green,
            Brushes.LightBlue,
            Brushes.Blue,
            Brushes.Navy,
            Brushes.Purple
        };
        public static Color[] colors = new Color[]
        {
            Colors.Red,
            Colors.OrangeRed,
            Colors.Orange,
            Colors.Yellow,
            Colors.YellowGreen,
            Colors.Green,
            Colors.LightBlue,
            Colors.Blue,
            Colors.Navy,
            Colors.Purple
        };
    }
}
