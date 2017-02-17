
import matplotlib, pylab
import collections

tstep = 0.03
mousepxratio = 40
ts = [x * tstep for x in range((int)(10 / tstep))] 

pylab.figure()

max_accel = 1000
def clamp(value, absmax):
	if abs(value) > absmax:
		return absmax if value > 0 else -absmax
	return value

State = collections.namedtuple('State', ['x', 'v', 'a'], verbose=True) # position, velocity, acceleration

def sim(v0, x0, controller):
	xs = [x0]*10
	vs = [v0]*10
	accs = [0]*10

	for t in ts[:-1]:
		xs.append(xs[-1] + vs[-2] * tstep)

		mousepx = controller(State(xs[-1], vs[-1], accs[-1]))
		clamp(mousepx, 20)
		a = accs[-1]*.8 + mousepx*mousepxratio
		accs.append(a)
		v = vs[-1] + a * tstep
		#v = clamp(v, 300)
		vs.append(vs[-1] + a * tstep)
	
	pylab.plot(ts, xs[0:len(ts)])

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
def initK(p):
	global K
	A = np.matrix([
		[ 0, tstep, tstep * tstep / 2 ], # position = old position + v Δt + a Δt^2 /2
        [ 0, -0.02, tstep ], # velocity = old velocity + a Δt, with slight damping in FA off (MUCH MORE IN FA-ON)
        [ 0, 0, -0.2 ], # acceleration = old acceleration * 0.8 (natural decay due to relative mouse)
    ]);
	B = np.matrix([[0],[0], [mousepxratio]]) # control directly modifies acceleration
	C = np.matrix([[1],[0], [0]]) # position is the only observable
	D = np.matrix([[0],[0], [0]]) # control does not bypass system to directly affect output

	# Define our costs:
	#p = 1 # controls how quickly we respond (measure of control effort vs. response time)
	Q = p * C * C.transpose()
	R = np.matrix([[1]]) # cost weighting of the various controllers. Only one so no effect.
	
	# Compute the LQR controller
	K, X, closedLoopEigVals = controlpy.synthesis.controller_lqr(A,B,Q,R)
	with open('C:/users/public/controllers.txt', 'a') as f:
		print("Control matrix for p={}: ".format(p), file=f)
		print(K, file=f)
		print(K)

def controllerModern(state, p=1):
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
	controlvector = - K * np.matrix(state).transpose()
	controlvalue = controlvector.item((0,0))
	return controlvalue

mousepxratio = 150
initK(p=3)
initK(p=1)
initK(p=0.5)

controller = controllerModern
for i in range(-100, 100, 10):
	sim(0, i, controller)
	sim(20, i, controller)
	sim(-20, i, controller)
	sim(-50, i, controller)
	sim(50, i, controller)

pylab.plot([ts[0], ts[-1]], [0, 0])
pylab.ylim([-10, 10])
pylab.show()