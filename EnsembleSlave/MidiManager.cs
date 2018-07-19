using NextMidi.DataElement;
using NextMidi.MidiPort.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnsembleSlave
{
    public class MidiManager
    {
        MidiOutPort port;

        public MidiManager()
        {
            port = new MidiOutPort(0);
            try
            {
                port.Open();
            }
            catch
            {
                Console.WriteLine("no such port exists");
                return;
            }
        }

        public void OnNote(byte note)
        {
            port.Send(new NoteEvent()
            {
                Note = note,
                Gate = 240,
            });
        }

        public void OffNote(byte note)
        {
            port.Send(new NoteOffEvent()
            {
                Note = note,
            });
        }

        public void ProgramChange(byte value)
        {
            port.Send(new ProgramEvent()
            {
                Value = value
            });
        }
    }
}
