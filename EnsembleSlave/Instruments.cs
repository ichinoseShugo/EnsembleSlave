using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnsembleSlave
{
    public class Instruments
    {
        public static byte ACOUSTIC_GRAND_PIANO = 0;
        public static byte MARIMBA = 12;
        public static byte ACOUSTIC_GUITAR_NYLON = 24;
        public static byte ACOUSTIC_BASE = 32;
        //public static byte VIOLIN = 40;
        public static byte TRUMPET = 56;
        //public static byte FLUTE = 73;

        public static string[] Names = new string[]
        {
            "ACOUSTIC_GRAND_PIANO",
            "MARIMBA",
            "ACOUSTIC_GUITAR_NYLON",
            "ACOUSTIC_BASE",
            //"VIOLIN",
            //"TRUMPET",
            //"FLUTE",
        };
        public static string[] JNames = new string[]
        {
            "グランドピアノ",
            "マリンバ",
            "ギター",
            "ベース",
            //"バイオリン",
            //"トランペット",
            //"フルート",
        };
        public static byte[] Numbers = new byte[]
        {
            0,
            12,
            24,
            32,
            //40,
            //56,
            //73,
        };
    }
}
