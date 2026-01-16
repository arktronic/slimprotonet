using SlimProtoNet.Protocol.Messages;

namespace SqueezeNetCli
{
    public static class Helpers
    {
        public static int GetSampleRate(PcmSampleRate rate)
        {
            switch (rate)
            {
                case PcmSampleRate.Rate11000: return 11000;
                case PcmSampleRate.Rate22000: return 22000;
                case PcmSampleRate.Rate32000: return 32000;
                case PcmSampleRate.Rate44100: return 44100;
                case PcmSampleRate.Rate48000: return 48000;
                case PcmSampleRate.Rate8000: return 8000;
                case PcmSampleRate.Rate12000: return 12000;
                case PcmSampleRate.Rate16000: return 16000;
                case PcmSampleRate.Rate24000: return 24000;
                case PcmSampleRate.Rate96000: return 96000;
                case PcmSampleRate.SelfDescribing:
                default: return 44100;
            }
        }

        public static int GetChannels(PcmChannels channels)
        {
            switch (channels)
            {
                case PcmChannels.Mono: return 1;
                case PcmChannels.Stereo: return 2;
                default: return 2;
            }
        }

        public static int GetBitsPerSample(PcmSampleSize size)
        {
            switch (size)
            {
                case PcmSampleSize.Eight: return 8;
                case PcmSampleSize.Sixteen: return 16;
                case PcmSampleSize.Twenty: return 20;
                case PcmSampleSize.ThirtyTwo: return 32;
                case PcmSampleSize.SelfDescribing:
                default: return 16;
            }
        }
    }
}
