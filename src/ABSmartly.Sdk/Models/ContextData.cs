﻿using ABSmartly.Extensions;

namespace ABSmartly.Models;

public class ContextData
{
    public ContextData()
    {
        
    }
    
    public ContextData(Experiment[] experiments)
    {
        Experiments = experiments;
    }

    public Experiment[] Experiments { get; set; }


    #region Overrides - Equality / Hash / ToString

    protected bool Equals(ContextData other)
    {
        return Equals(Experiments, other.Experiments);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((ContextData)obj);
    }

    public override int GetHashCode()
    {
        return Experiments != null ? Experiments.GetHashCode() : 0;
    }

    public override string ToString()
    {
        return "ContextData{" +
               "experiments=" + Experiments.ToArrayString() +
               '}';
    }

    #endregion
}