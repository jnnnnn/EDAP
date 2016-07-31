import sys
import cv2
import math
import numpy
from scipy.ndimage import label
pi_4 = 4*math.pi

def nothing_asCallback(x):
    pass

def GUI_openCV_circles():
    frame = cv2.imread(  "../compass_tests_red.png" )
    demo  = frame[:800,:800,:]
    cv2.namedWindow( "DEMO.IN",           )
    cv2.namedWindow( "DEMO.Canny",        )
    cv2.namedWindow( "DEMO.Canny.Circles",)
    aKeyPRESSED                                     = None              # .init

    aCanny_LoTreshold                               = 127
    aCanny_LoTreshold_PREVIOUS                      =  -1
    aCanny_HiTreshold                               = 255
    aCanny_HiTreshold_PREVIOUS                      =  -1

    aHough_dp                                       =   1
    aHough_dp_PREVIOUS                              =  -1
    aHough_minDistance                              =  10
    aHough_minDistance_PREVIOUS                     =  -1
    aHough_param1_aCannyHiTreshold                  = 255
    aHough_param1_aCannyHiTreshold_PREVIOUS         =  -1
    aHough_param2_aCentreDetectTreshold             =  20
    aHough_param2_aCentreDetectTreshold_PREVIOUS    =  -1
    aHough_minRadius                                =  10
    aHough_minRadius_PREVIOUS                       =  -1
    aHough_maxRadius                                =  30
    aHough_maxRadius_PREVIOUS                       =  -1
    cv2.createTrackbar( "Lo_Treshold",          "DEMO.Canny",          aCanny_LoTreshold,                      1000, nothing_asCallback )
    cv2.createTrackbar( "Hi_Treshold",          "DEMO.Canny",          aCanny_HiTreshold,                      1000, nothing_asCallback )

    cv2.createTrackbar( "dp",                   "DEMO.Canny.Circles",  aHough_dp,                              100, nothing_asCallback )
    cv2.createTrackbar( "minDistance",          "DEMO.Canny.Circles",  aHough_minDistance,                     100, nothing_asCallback )
    cv2.createTrackbar( "param1_HiTreshold",    "DEMO.Canny.Circles",  aHough_param1_aCannyHiTreshold,         1000, nothing_asCallback )
    cv2.createTrackbar( "param2_CentreDetect",  "DEMO.Canny.Circles",  aHough_param2_aCentreDetectTreshold,    255, nothing_asCallback )
    cv2.createTrackbar( "minRadius",            "DEMO.Canny.Circles",  aHough_minRadius,                       40, nothing_asCallback )
    cv2.createTrackbar( "maxRadius",            "DEMO.Canny.Circles",  aHough_maxRadius,                       40, nothing_asCallback )

    cv2.imshow( "DEMO.IN",          demo )                              # static ...
    while( True ):
        if aKeyPRESSED == 27:
            break
        aCanny_LoTreshold = cv2.getTrackbarPos( "Lo_Treshold", "DEMO.Canny" )
        aCanny_HiTreshold = cv2.getTrackbarPos( "Hi_Treshold", "DEMO.Canny" )

        if (    aCanny_LoTreshold      != aCanny_LoTreshold_PREVIOUS
            or  aCanny_HiTreshold      != aCanny_HiTreshold_PREVIOUS
            ):
            aCannyRefreshFLAG           = True
            aCanny_LoTreshold_PREVIOUS  = aCanny_LoTreshold
            aCanny_HiTreshold_PREVIOUS  = aCanny_HiTreshold
        else:
            aCannyRefreshFLAG           = False

        aHough_dp                           = 0.1*cv2.getTrackbarPos( "dp",                 "DEMO.Canny.Circles" )
        aHough_minDistance                  = cv2.getTrackbarPos( "minDistance",        "DEMO.Canny.Circles" )
        aHough_param1_aCannyHiTreshold      = cv2.getTrackbarPos( "param1_HiTreshold",  "DEMO.Canny.Circles" )
        aHough_param2_aCentreDetectTreshold = cv2.getTrackbarPos( "param2_CentreDetect","DEMO.Canny.Circles" )
        aHough_minRadius                    = cv2.getTrackbarPos( "minRadius",          "DEMO.Canny.Circles" )
        aHough_maxRadius                    = cv2.getTrackbarPos( "maxRadius",          "DEMO.Canny.Circles" )

        if (    aHough_dp                            != aHough_dp_PREVIOUS
            or  aHough_minDistance                   != aHough_minDistance_PREVIOUS
            or  aHough_param1_aCannyHiTreshold       != aHough_param1_aCannyHiTreshold_PREVIOUS
            or  aHough_param2_aCentreDetectTreshold  != aHough_param2_aCentreDetectTreshold_PREVIOUS    
            or  aHough_minRadius                     != aHough_minRadius_PREVIOUS
            or  aHough_maxRadius                     != aHough_maxRadius_PREVIOUS
            ):
            aHoughRefreshFLAG           = True                  
            aHough_dp_PREVIOUS                              =  aHough_dp                          
            aHough_minDistance_PREVIOUS                     =  aHough_minDistance                 
            aHough_param1_aCannyHiTreshold_PREVIOUS         =  aHough_param1_aCannyHiTreshold     
            aHough_param2_aCentreDetectTreshold_PREVIOUS    =  aHough_param2_aCentreDetectTreshold
            aHough_minRadius_PREVIOUS                       =  aHough_minRadius                   
            aHough_maxRadius_PREVIOUS                       =  aHough_maxRadius                   
        else:
            aHoughRefreshFLAG           = False
        if ( aCannyRefreshFLAG ):
            edges   = cv2.Canny(        demo,   aCanny_LoTreshold,
                                                aCanny_HiTreshold
                                        )
            cv2.imshow( "DEMO.Canny",   edges )

        if ( aCannyRefreshFLAG or aHoughRefreshFLAG ):
            try:
                circles = cv2.HoughCircles( edges,  cv2.HOUGH_GRADIENT,
                                                    aHough_dp,
                                                    aHough_minDistance,
                                                    param1      = aHough_param1_aCannyHiTreshold,
                                                    param2      = aHough_param2_aCentreDetectTreshold,
                                                    minRadius   = aHough_minRadius,
                                                    maxRadius   = aHough_maxRadius
                                            )
                demoWithCircles = cv2.cvtColor( demo,            cv2.COLOR_BGR2RGB )                          # .re-init <<< src
                demoWithCircles = cv2.cvtColor( demoWithCircles, cv2.COLOR_RGB2BGR )
                for aCircle in circles[0]:
                    cv2.circle( demoWithCircles,    ( int( aCircle[0] ), int( aCircle[1] ) ),
                                                    aCircle[2],
                                                    (0,255,0),
                                                    1
                                )                
                cv2.imshow( "DEMO.Canny.Circles2", demoWithCircles )
            except Exception as e:
                print(e);
        # ref. above in .onRefreshFLAG RE-SYNC sections
        aKeyPRESSED = cv2.waitKey(1) & 0xFF
    cv2.destroyWindow( "DEMO.IN" )
    cv2.destroyWindow( "DEMO.Canny" )
    cv2.destroyWindow( "DEMO.Canny.Circles" )

    cv2.destroyWindow( "DEMO.Canny.Circles2" )

def main():
    GUI_openCV_circles()
    return 0

if __name__ == '__main__':
    main()