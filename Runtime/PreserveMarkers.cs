using System;

namespace UnityOBU
{
    // IL2CPP strips aggressively at High. Anything only referenced through reflection,
    // SendMessage, JSON binding or a native callback can vanish. Tag those members so the
    // link.xml generator in the Editor picks them up, and so the intent is visible in code.
    //
    // UnityEngine.Scripting.PreserveAttribute already protects a member from stripping. This
    // marker is narrower: it tells the OBU link.xml step to also list the member explicitly,
    // which survives assembly renames and is easier to audit than scattered [Preserve] tags.
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method |
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor,
        Inherited = false)]
    public sealed class KeepAttribute : Attribute
    {
        public string Reason { get; }

        public KeepAttribute(string reason = null)
        {
            Reason = reason;
        }
    }
}
