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
        public static byte MUSIC_BOX = 10;
        public static byte VIBRAPHONE = 11;
        public static byte MARIMBA = 12;
        public static byte ACOUSTIC_GUITAR_NYLON = 24;
        public static byte ACOUSTIC_BASE = 32;
        //public static byte VIOLIN = 40;
        public static byte TRUMPET = 56;
        //public static byte FLUTE = 73;
        public static byte WOODBLOCK = 115;

        public static string[] Names = new string[]
        {
            "ACOUSTIC_GRAND_PIANO",
            "MUSIC_BOX",
            "VIBRAPHONE",
            "MARIMBA",
            "ACOUSTIC_GUITAR_NYLON",
            //"ACOUSTIC_BASE",
            //"VIOLIN",
            //"TRUMPET",
            //"FLUTE",
            "WOODBLOCK",
        };
        public static string[] JNames = new string[]
        {
            "グランドピアノ",
            "オルゴール",
            "ビブラフォン",
            "マリンバ",
            "ギター",
            //"ベース",
            //"バイオリン",
            //"トランペット",
            //"フルート",
            "ウッドブロック",
        };
        public static byte[] Numbers = new byte[]
        {
            0,
            10,
            11,
            12,
            24,
            //32,
            //40,
            //56,
            //73,
            115,
        };
    }
}
