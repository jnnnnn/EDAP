using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDAP
{
    /// <summary>
    /// This class is for logging in and out of the game to reset mission boards, barn acles, skim mers, generators, and whatever else.
    /// </summary>
    class Relogger
    {
        public Keyboard keyboard;
        public MenuSensor menuSensor;

        DateTime loadStartWaiting = DateTime.UtcNow;

        [Flags]
        public enum MenuState
        {
            None = 0,
            ExitToMainMenuStart = 1 << 0,
            YesExit = 1 << 1,
            Start = 1 << 2,
            ChooseMode = 1 << 3,
            Solo = 1 << 4,
            StationMenu = 1 << 5,
            LandedMenu = 1 << 6,
            MissionBoardSelected = 1 << 7,
            Enabled = 1 << 8,
        }
        public MenuState state;
        
        public void Act()
        {            
            if (!state.HasFlag(MenuState.ExitToMainMenuStart))
            {
                // exit to main menu
                keyboard.Tap(SendInput.ScanCode.ESC);
                Thread.Sleep(1000); // wait for the menu to come up
                keyboard.TapWait(SendInput.ScanCode.KEY_W);
                keyboard.TapWait(SendInput.ScanCode.KEY_W);
                keyboard.TapWait(SendInput.ScanCode.SPACEBAR); // quit to menu
                Thread.Sleep(800); // wait a bit longer for the menu to load.
                keyboard.TapWait(SendInput.ScanCode.KEY_D); // select the yes option
                state |= MenuState.ExitToMainMenuStart;
                return;
            }

            if (!state.HasFlag(MenuState.YesExit))
            {
                // in case there is a logout timer, wait until yes goes white
                if (menuSensor.MatchScreen(menuSensor.template_yes))
                {
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR); // confirm
                    state |= MenuState.YesExit;
                }
                return;
            }

            if (!state.HasFlag(MenuState.Start))
            {
                if (menuSensor.MatchScreen(menuSensor.template_start))
                {
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR); // play game                    
                    state |= MenuState.Start;
                }
                return;
            }
            
            if (!state.HasFlag(MenuState.ChooseMode))
            {
                Thread.Sleep(1000); // give the menu time to open
                if (state.HasFlag(MenuState.Solo))
                {
                    // choose solo
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR);
                }
                else
                {
                    // choose first private group
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR);
                    Thread.Sleep(500); // wait for list of groups to open
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR);
                }                
                state |= MenuState.ChooseMode;                
                return;
            }            
            
            if (!state.HasFlag(MenuState.LandedMenu))
            {
                // todo: this isn't matching for some reason, gets stuck here
                if (menuSensor.MatchScreen(menuSensor.template_landedmenu))
                {
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR); // select Return To Surface
                    keyboard.TapWait(SendInput.ScanCode.KEY_W);
                    keyboard.TapWait(SendInput.ScanCode.SPACEBAR); // select Station Services

                    state |= MenuState.LandedMenu;
                }
                return;
            }
            
            if (!state.HasFlag(MenuState.StationMenu))
            {
                // Wait for the station menu to open
                if (menuSensor.MatchScreen(menuSensor.template_mission_unselected) || menuSensor.MatchScreen(menuSensor.template_mission_selected))
                    state |= MenuState.StationMenu;
                return;
            }

            if (!state.HasFlag(MenuState.MissionBoardSelected))
            { 
                // scroll down to mission board
                if (!menuSensor.MatchScreen(menuSensor.template_mission_selected))
                {
                    keyboard.TapWait(SendInput.ScanCode.KEY_S);
                    
                }
                else
                {
                    keyboard.Tap(SendInput.ScanCode.SPACEBAR); // open mission board
                    state |= MenuState.MissionBoardSelected;
                    
                }
                return;
            }

            state = MenuState.None;  
        }
    }
}
