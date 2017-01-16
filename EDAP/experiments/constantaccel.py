import matplotlib, pylab


tstep = 0.001
ts = [x / 1000 for x in range(5000)] 

pylab.figure()

def sign(n): return 1 if n > 0 else -1

def sim(v, x):
	a = 720.;		
	# work out the initial acceleration direction
	# 1. find the acceleration direction that will give us a stationary point
	if a * v >= 0: a *= -1
	inflection = x - v*v/(2*a)
	if inflection * x >= 0:
		a = abs(a) * -sign(x)
	else:
		a = abs(a) * sign(x)

	rootpart = 1/(a*2**.5) * (v*v - 2*a*x)**.5
	t1 = -v/a + rootpart;
	t2 = -v/a - rootpart;
	print (("{:0.3} "*2).format(t1, t2))
	
	t0 = max(t1, t2)
	xs = [x]
	vs = [v]
	for t in ts[:-1]:
		xs.append(xs[-1] + vs[-1] * tstep)
		vs.append(vs[-1] + a * ((t < t0)*2-1) * tstep)
	
	pylab.plot(ts, xs)

#sim(700,-200)
for i in range(-1001, 1000, 100):
	sim(-700, i)
	sim( 700, i)
#pylab.plot([ts[0], ts[-1]], [0, 0])
pylab.show()