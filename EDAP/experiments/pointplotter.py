import matplotlib, pylab

values = []
for line in open(r"c:\users\public\controller3.txt", 'r'):
	values.append([float(v) for v in line.split(", ")])

for series in list(zip(*values)):
	pylab.plot(series)

pylab.plot([0, len(series)], [0, 0])
pylab.show()