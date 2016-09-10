import matplotlib, pylab


tstep = 0.001
ts = [x / 1000 for x in range(2000)] 

pylab.figure()

def sim(v, x):
	a = 720.;	
	if v*v - 2*a*x < 0:
		a *= -1; # make sure sqrt is not imaginary
	t1 = -v/a + 1/(a*2**.5) * (v*v - 2*a*x)**.5;
	t2 = -v/a - 1/(a*2**.5) * (v*v - 2*a*x)**.5;
	print (t1, t2)
	
	t0 = max(t1, t2)
	xs = [x]
	vs = [v]
	for t in ts[:-1]:
		xs.append(xs[-1] + vs[-1] * tstep)
		vs.append(vs[-1] + a * ((t < t0)*2-1) * tstep)
	
	pylab.plot(ts, xs)

sim(-142, 150)
sim(0, 150)
sim(0, 0)
sim(-100, 0)
sim(142, -150)
sim(-40., 300.)

pylab.plot([ts[0], ts[-1]], [0, 0])
pylab.show()