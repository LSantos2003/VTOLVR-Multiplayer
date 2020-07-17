﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Serializable]
public class Message_Ready : Message
{
    public ulong UID;
    public bool isHost;

    public Message_Ready(ulong uID, bool isHost) {
        UID = uID;
        this.isHost = isHost;
        type = MessageType.Ready;
    }
}
