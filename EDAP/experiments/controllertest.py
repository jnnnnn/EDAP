import matplotlib, pylab

tstep = 0.03
ts = [x * tstep for x in range((int)(5 / tstep))] 

pylab.figure()

max_accel = 2000
def clamp(value, absmax):
	if abs(value) > absmax:
		return absmax if value > 0 else -absmax
	return value

def sim(v0, x0, controller):

	xs = [x0]
	vs = [v0,v0]

	for t in ts[:-1]:
		xs.append(xs[-1] + vs[-1] * tstep)

		a = controller(vs[-2], xs[-1])
		a = clamp(a, max_accel)
		v = vs[-1] + a * tstep
		v = clamp(v, 300)
		vs.append(vs[-1] + a * tstep)
	
	pylab.plot(ts, xs)

def controllerPlacebo(v, x):
	return -x

def controllerLinear(v, x):
	"""return the desired acceleration for the next timestep"""
	desiredVelocity = -x
	desireda = (desiredVelocity - v)/tstep
	return desireda

def controllerQuadratic(v, x, amax=max_accel):
	"""return the desired acceleration for the next timestep"""
	a = amax;		
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
	return a

def controllerQuadraticDamped(v, x):	
	if abs(x) < 2:
		return controllerLinear(v, x)

	return controllerQuadratic(v, x)
	
controller = controllerQuadraticDamped
for i in range(-500, 500, 100):
	sim(0, i, controller)
	sim(200, i, controller)
	sim(-200, i, controller)
	sim(-500, i, controller)
	sim(500, i, controller)

pylab.plot([ts[0], ts[-1]], [0, 0])
pylab.ylim([-30, 30])
pylab.show()