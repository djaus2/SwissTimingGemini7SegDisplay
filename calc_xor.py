s = 'Q,-000.02,+000.01,M,00,'
result = 0
for c in s:
    result ^= ord(c)
    print(f'{c}: {ord(c):3d} (0x{ord(c):02X}) -> result: {result} (0x{result:02X})')
print(f'\nFinal XOR result: {result} (0x{result:02X})')
