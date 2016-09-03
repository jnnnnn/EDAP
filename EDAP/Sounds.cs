using System.Runtime.InteropServices;

namespace EDAP
{
    /// <summary>
    /// Play sounds. I got them from http://www.oddca st.com/home/demos/tts/tts_exam ple.php?site pal
    /// </summary>
    class Sounds
    {
        [DllImport("winmm.dll")]
        public static extern uint mciSendString(string lpstrCommand, string lpstrReturnString, uint uReturnLength, uint hWndCallback);

        public static void Play(string name)
        {
            mciSendString(@"close temp_alias", null, 0, 0);
            string playcommand = string.Format(@"open ""{0}"" alias temp_alias", name);
            mciSendString(playcommand, null, 0, 0);
            mciSendString("play temp_alias", null, 0, 0);
        }

        public static void PlayOneOf(params string[] names)
        {
            Play(names[new System.Random().Next(0, names.Length)]);
        }
    }
}
