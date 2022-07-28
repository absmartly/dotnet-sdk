﻿using ABSmartly.Json;

namespace ABSmartly.Interfaces;

public interface IContextDataDeserializer
{
    ContextData Deserialize(byte[] bytes, int offset, int length);
}