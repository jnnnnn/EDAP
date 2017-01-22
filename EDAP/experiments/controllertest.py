
import matplotlib, pylab
import collections

tstep = 0.03
ts = [x * tstep for x in range((int)(10 / tstep))] 

pylab.figure()

max_accel = 1000
def clamp(value, absmax):
	if abs(value) > absmax:
		return absmax if value > 0 else -absmax
	return value

State = collections.namedtuple('State', ['x', 'v', 'a'], verbose=True) # position, velocity, acceleration

def sim(v0, x0, controller):
	xs = [x0]
	vs = [v0,v0]
	accs = [0,0,0,0]

	for t in ts[:-1]:
		xs.append(xs[-1] + vs[-2] * tstep)

		a = controller(State(xs[-1], vs[-2], accs[-4]))
		#a = clamp(a, max_accel)
		v = vs[-1] + a * tstep
		#v = clamp(v, 300)
		vs.append(vs[-1] + a * tstep)
	
	pylab.plot(ts, xs)

def controllerPlacebo(state):
	return -state.x

def controllerLinear(state):
	"""return the desired acceleration for the next timestep"""
	desiredVelocity = -state.x
	desireda = (desiredVelocity - state.v)/tstep
	return desireda

def controllerQuadratic(state, amax=max_accel):
	"""This is a controller implemented for a second-order system before I learned about:
	 - PIDs
	 - State-space representation
	 - libraries like controlpy that can automatically generate an lqr controller

	It is effectively an attempt at perfectly timing a bang-bang controller using (almost) perfect information and perfect control.
	"""
	a = amax/3;		
	x = state.x
	v = state.v
	# work out the initial acceleration direction
	# 1. find the acceleration direction that will give us a stationary point
	if a * v >= 0: a *= -1
	inflection = x - v*v/(2*a)
	# 2. If the inflection is already past the target, start by decelerating
	signx = 1 if x >= 0 else -1
	if inflection * x >= 0:
		a = abs(a) * -signx
	else:
		a = abs(a) * signx

	rootpart = 1/(a*2**.5) * (v*v - 2*a*x)**.5
	t1 = -v/a + rootpart;
	t2 = -v/a - rootpart;	
	t0 = max(t1, t2)
	if t0 > 2*tstep:
		return a
	return a * (t0 / tstep)

def controllerQuadraticDamped(state):	
	x, v, a = state
	if abs(x) < 1:
		return controllerLinear(v, x)

	a = controllerQuadratic(v, x)
	if v*x < 0 and a*v > 0:
		a *= 0.5
	return a

import controlpy
import numpy as np
K = None
def controllerModern(state):
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
	global K
	if K is None:
		timedelta = 1
		A = np.matrix([
			[ 0, timedelta, timedelta * timedelta / 2 ], # position = old position + v Δt + a Δt^2 /2
	        [ 0, -0.02, timedelta ], # velocity = old velocity + a Δt, with slight damping in FA off (MUCH MORE IN FA-ON)
	        [ 0, 0, -0.2 ], # acceleration = old acceleration * 0.9 (natural decay due to relative mouse)
	    ]);
		B = np.matrix([[0],[0],[1]]) # control directly modifies acceleration
		C = np.matrix([[1],[0], [0]]) # position is the only observable
		D = np.matrix([[0],[0], [0]]) # control does not bypass system to directly affect output

		# Define our costs:
		p = 10 # controls how quickly we respond (measure of control effort vs. response time)
		Q = p * C * C.transpose()
		R = np.matrix([[1]]) # no idea what this is
		
		# Compute the LQR controller
		K, X, closedLoopEigVals = controlpy.synthesis.controller_lqr(A,B,Q,R)
		print("Control matrix: ")
		print(K)

	controlvector = - K * np.matrix(state).transpose()
	controlvalue = controlvector.item((0,0))
	return controlvalue

	
controller = controllerModern
for i in range(-500, 500, 100):
	sim(0, i, controller)
	sim(200, i, controller)
	sim(-200, i, controller)
	sim(-500, i, controller)
	sim(500, i, controller)

pylab.plot([ts[0], ts[-1]], [0, 0])
pylab.ylim([-30, 30])
pylab.show()