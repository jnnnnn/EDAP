import matplotlib, pylab

class Controller():
	def __init__(self):
		self.accumulated_error = 0
		self.previous_error = 0
		self.acted = False
	def step(self, measured_position, v, a):
		if not self.acted:
			self.acted = True
			return -0.1
		return 0
		error = measured_position
		self.accumulated_error += error
		velocity = error - self.previous_error
		self.previous_error = error		
		return 0.0001*error #+ 0.0001 * self.accumulated_error + 0.0001 * velocity

xs = [30]*5
vs = [0]*5
accs = [0]*5
controls = [0]*5

controller = Controller()

for i in range(1000):
	xs.append(xs[-1] + vs[-1])
	vs.append(vs[-1]*0.98 + accs[-1]) # slight velocity damping
	accs.append(accs[-1] * 0.9 + controls[-1]) # significant acceleration damping, control affects acceleration after lag
	controls.append(controller.step(xs[-1], vs[-1], accs[-1])) # measurement delay, velocity/acceleration prediction is accurate

pylab.plot(xs)
pylab.plot(vs)
pylab.plot(accs)
pylab.plot(controls)

pylab.ylim(-100, 100)

pylab.show()