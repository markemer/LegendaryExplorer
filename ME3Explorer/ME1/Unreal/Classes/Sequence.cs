﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME1Explorer.Unreal.Classes
{
    public class Sequence
    {
        public ME1Package pcc;
        public int index;
        public List<PropertyReader.Property> props;
        public List<int> SequenceObjects;

        public Sequence(ME1Package Pcc, int export)
        {
            pcc = Pcc;
            index = export;
            Deserialize();
        }

        public void Deserialize()
        {
            props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            getSequenceObjects();
        }

        public void getSequenceObjects()
        {
            if (props == null || props.Count == 0)
                return;
            for (int i = 0; i < props.Count(); i++)
                if (pcc.getNameEntry(props[i].Name) == "SequenceObjects")
                {
                    SequenceObjects = new List<int>();
                    byte[] buff = props[i].raw;
                    int count = BitConverter.ToInt32(buff, 24);
                    for (int j = 0; j < count; j++)
                        SequenceObjects.Add(BitConverter.ToInt32(buff, 28 + j * 4));
                    SequenceObjects.Sort();
                }
        }
    }
}
