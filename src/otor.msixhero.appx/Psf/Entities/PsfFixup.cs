﻿using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Otor.MsixHero.Appx.Psf.Entities
{
    [DataContract]
    public class PsfFixup : JsonElement
    {
        [DataMember(Name = "dll")]
        public string Dll { get; set; }

        [JsonIgnore]
        public PsfFixupConfig Config { get; set; }
        
        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            if (this.Config == null)
            {
                this.rawValues = null;
                return;
            }

            this.rawValues = JObject.FromObject(this.Config);
        }

        [OnDeserialized]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            if (!this.rawValues.ContainsKey("config"))
            {
                return;
            }

            if (this.Dll?.StartsWith("FileRedirectionFixup", StringComparison.OrdinalIgnoreCase) == true)
            {
                this.Config = JsonConvert.DeserializeObject<PsfRedirectionFixupConfig>(this.rawValues["config"].ToString(Formatting.None));
            }
            else if (this.Dll?.StartsWith("TraceFixup", StringComparison.OrdinalIgnoreCase) == true)
            {
                this.Config = JsonConvert.DeserializeObject<PsfTraceFixupConfig>(this.rawValues["config"].ToString(Formatting.None));
            }
            else if (this.Dll?.StartsWith("ElectronFixup", StringComparison.OrdinalIgnoreCase) == true)
            {
                this.Config = JsonConvert.DeserializeObject<PsfElectronFixupConfig>(this.rawValues["config"].ToString(Formatting.None));
            }
            else
            {
                this.Config = new CustomPsfFixupConfig(this.rawValues["config"].ToString(Formatting.Indented));
            }

            this.rawValues.Remove("config");
        }

        public override string ToString()
        {
            return this.Dll;
        }
    }
}