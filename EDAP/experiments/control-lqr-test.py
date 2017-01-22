'''A simple example script, which implements an LQR controller for a double integrator.
'''

from __future__ import print_function, division

import controlpy

import numpy as np

"""
Got my shit together and learned control theory:
	control theory https://www.cds.caltech.edu/~murray/courses/cds101/fa02/caltech/astrom-ch4.pdf
	http://www-bcf.usc.edu/~ioannou/RobustAdaptiveBook95pdf/Robust_Adaptive_Control.pdf

    https://en.wikipedia.org/wiki/State-space_representation
    https://en.wikipedia.org/wiki/Linear%E2%80%93quadratic_regulator
    http://www.mwm.im/python-control-library-controlpy/

    http://robotics.stackexchange.com/questions/210/how-can-i-automatically-adjust-pid-parameters-on-the-fly?rq=1
    http://electronics.stackexchange.com/questions/50049/how-to-implement-a-self-tuning-pid-like-controller?rq=1
    https://www.researchgate.net/post/How_can_I_design_a_PID_controller_to_stabilize_a_4th_order_system

    http://ctms.engin.umich.edu/CTMS/index.php?example=AircraftPitch&section=ControlStateSpace

"""
timedelta = 1
A = np.matrix([
	[ 0, timedelta, timedelta * timedelta / 2 ], # position = old position + v Δt + a Δt^2 /2
    [ 0, -0.02, timedelta ], # velocity = old velocity + a Δt, with slight damping in FA off (MUCH MORE IN FA-ON)
    [ 0, 0, -0.2 ], # acceleration = old acceleration * 0.9 (natural decay due to relative mouse)
]);
B = np.matrix([[0],[0],[0.02]]) # control directly modifies acceleration
C = np.matrix([[1],[0], [0]]) # position is the only observable
D = np.matrix([[0],[0], [0]]) # control does not bypass system to directly affect output

# Define our costs:
p = 2 # controls how quickly we respond (measure of control effort vs. response time)
Q = p * C * C.transpose()
R = np.matrix([[1]]) # no idea what this is

# Compute the LQR controller
gain, X, closedLoopEigVals = controlpy.synthesis.controller_lqr(A,B,Q,R)


print('The computed gain is:')
print(gain)

print('The closed loop eigenvalues are:')
print(closedLoopEigVals)

print('X is')
print(X)