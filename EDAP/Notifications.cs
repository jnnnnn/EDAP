using Discord;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Linq;

namespace EDAP
{
    class Notifications
    {
        public BufferBlock<string> messages = new BufferBlock<string>(); // add a message to this queue and the bot will send it when possible
                
        public async void Start()
        {
            try
            {
                DiscordClient discord = new DiscordClient();
                await discord.Connect("bot token goes here", TokenType.Bot);
                ulong userid = 1111111111111111; // (type \@username#number in discord to get this)
                Channel channel = await discord.CreatePrivateChannel(userid);
                await channel.SendMessage("EDAP Online");
                while (true)
                {
                    string message = await messages.ReceiveAsync();
                    await channel.SendMessage("@username#number " + message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                // if discord isn't working, don't crash
            }
        }
    }
}
