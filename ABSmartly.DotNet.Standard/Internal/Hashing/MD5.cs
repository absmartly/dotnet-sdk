﻿using System;
using System.Threading;

namespace ABSmartly.Internal.Hashing;

public class MD5
{
    public static byte[] digestBase64UrlNoPadding(byte[] key, int offset, int len) {
        int[] state = md5state(key, offset, len);

        int a = state[0];
        int b = state[1];
        int c = state[2];
        int d = state[3];

        byte[] result = new byte[22];

        int t = a;
        result[0] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[1] = Base64URLNoPaddingChars[((t & 3) << 4) | ((((int)((uint)t >> 12))) & 15)];
        result[2] = Base64URLNoPaddingChars[((((int)((uint)t >> 8)) & 15) << 2) | (((int)((uint)t >> 22)) & 3)];
        result[3] = Base64URLNoPaddingChars[((int)((uint)t >> 16)) & 63];

        t = ((int)((uint)a >> 24)) | (b << 8);
        result[4] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[5] = Base64URLNoPaddingChars[((t & 3) << 4) | ((((int)((uint)t >> 12))) & 15)];
        result[6] = Base64URLNoPaddingChars[((((int)((uint)t >> 8)) & 15) << 2) | (((int)((uint)t >> 22)) & 3)];
        result[7] = Base64URLNoPaddingChars[((int)((uint)t >> 16)) & 63];

        t = ((int)((uint)b >> 16)) | (c << 16);
        result[8] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[9] = Base64URLNoPaddingChars[((t & 3) << 4) | ((((int)((uint)t >> 12))) & 15)];
        result[10] = Base64URLNoPaddingChars[((((int)((uint)t >> 8)) & 15) << 2) | (((int)((uint)t >> 22)) & 3)];
        result[11] = Base64URLNoPaddingChars[((int)((uint)t >> 16)) & 63];

        t = (int)((uint)c >> 8);
        result[12] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[13] = Base64URLNoPaddingChars[((t & 3) << 4) | ((((int)((uint)t >> 12))) & 15)];
        result[14] = Base64URLNoPaddingChars[((((int)((uint)t >> 8)) & 15) << 2) | (((int)((uint)t >> 22)) & 3)];
        result[15] = Base64URLNoPaddingChars[((int)((uint)t >> 16)) & 63];

        t = d;
        result[16] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[17] = Base64URLNoPaddingChars[((t & 3) << 4) | ((((int)((uint)t >> 12))) & 15)];
        result[18] = Base64URLNoPaddingChars[((((int)((uint)t >> 8)) & 15) << 2) | (((int)((uint)t >> 22)) & 3)];
        result[19] = Base64URLNoPaddingChars[((int)((uint)t >> 16)) & 63];

        t = (int)((uint)d >> 24);
        result[20] = Base64URLNoPaddingChars[((int)((uint)t >> 2)) & 63];
        result[21] = Base64URLNoPaddingChars[(t & 3) << 4];

        return result;
    }

    	private static int cmn(int q, int a, int b, int x, int s, int t) {
		a = a + q + x + t;
        // Todo: https://www.geeksforgeeks.org/integer-rotateleft-method-in-java/
		return Integer.rotateLeft(a, s) + b;
	}

	private static int ff(int a, int b, int c, int d, int x, int s, int t) {
		return cmn((b & c) | (~b & d), a, b, x, s, t);
	}

	private static int gg(int a, int b, int c, int d, int x, int s, int t) {
		return cmn((b & d) | (c & ~d), a, b, x, s, t);
	}

	private static int hh(int a, int b, int c, int d, int x, int s, int t) {
		return cmn(b ^ c ^ d, a, b, x, s, t);
	}

	private static int ii(int a, int b, int c, int d, int x, int s, int t) {
		return cmn(c ^ (b | ~d), a, b, x, s, t);
	}

	private static void md5cycle(int[] x, int[] k) {
		int a = x[0];
		int b = x[1];
		int c = x[2];
		int d = x[3];

		a = ff(a, b, c, d, k[0], 7, -680876936);
		d = ff(d, a, b, c, k[1], 12, -389564586);
		c = ff(c, d, a, b, k[2], 17, 606105819);
		b = ff(b, c, d, a, k[3], 22, -1044525330);
		a = ff(a, b, c, d, k[4], 7, -176418897);
		d = ff(d, a, b, c, k[5], 12, 1200080426);
		c = ff(c, d, a, b, k[6], 17, -1473231341);
		b = ff(b, c, d, a, k[7], 22, -45705983);
		a = ff(a, b, c, d, k[8], 7, 1770035416);
		d = ff(d, a, b, c, k[9], 12, -1958414417);
		c = ff(c, d, a, b, k[10], 17, -42063);
		b = ff(b, c, d, a, k[11], 22, -1990404162);
		a = ff(a, b, c, d, k[12], 7, 1804603682);
		d = ff(d, a, b, c, k[13], 12, -40341101);
		c = ff(c, d, a, b, k[14], 17, -1502002290);
		b = ff(b, c, d, a, k[15], 22, 1236535329);

		a = gg(a, b, c, d, k[1], 5, -165796510);
		d = gg(d, a, b, c, k[6], 9, -1069501632);
		c = gg(c, d, a, b, k[11], 14, 643717713);
		b = gg(b, c, d, a, k[0], 20, -373897302);
		a = gg(a, b, c, d, k[5], 5, -701558691);
		d = gg(d, a, b, c, k[10], 9, 38016083);
		c = gg(c, d, a, b, k[15], 14, -660478335);
		b = gg(b, c, d, a, k[4], 20, -405537848);
		a = gg(a, b, c, d, k[9], 5, 568446438);
		d = gg(d, a, b, c, k[14], 9, -1019803690);
		c = gg(c, d, a, b, k[3], 14, -187363961);
		b = gg(b, c, d, a, k[8], 20, 1163531501);
		a = gg(a, b, c, d, k[13], 5, -1444681467);
		d = gg(d, a, b, c, k[2], 9, -51403784);
		c = gg(c, d, a, b, k[7], 14, 1735328473);
		b = gg(b, c, d, a, k[12], 20, -1926607734);

		a = hh(a, b, c, d, k[5], 4, -378558);
		d = hh(d, a, b, c, k[8], 11, -2022574463);
		c = hh(c, d, a, b, k[11], 16, 1839030562);
		b = hh(b, c, d, a, k[14], 23, -35309556);
		a = hh(a, b, c, d, k[1], 4, -1530992060);
		d = hh(d, a, b, c, k[4], 11, 1272893353);
		c = hh(c, d, a, b, k[7], 16, -155497632);
		b = hh(b, c, d, a, k[10], 23, -1094730640);
		a = hh(a, b, c, d, k[13], 4, 681279174);
		d = hh(d, a, b, c, k[0], 11, -358537222);
		c = hh(c, d, a, b, k[3], 16, -722521979);
		b = hh(b, c, d, a, k[6], 23, 76029189);
		a = hh(a, b, c, d, k[9], 4, -640364487);
		d = hh(d, a, b, c, k[12], 11, -421815835);
		c = hh(c, d, a, b, k[15], 16, 530742520);
		b = hh(b, c, d, a, k[2], 23, -995338651);

		a = ii(a, b, c, d, k[0], 6, -198630844);
		d = ii(d, a, b, c, k[7], 10, 1126891415);
		c = ii(c, d, a, b, k[14], 15, -1416354905);
		b = ii(b, c, d, a, k[5], 21, -57434055);
		a = ii(a, b, c, d, k[12], 6, 1700485571);
		d = ii(d, a, b, c, k[3], 10, -1894986606);
		c = ii(c, d, a, b, k[10], 15, -1051523);
		b = ii(b, c, d, a, k[1], 21, -2054922799);
		a = ii(a, b, c, d, k[8], 6, 1873313359);
		d = ii(d, a, b, c, k[15], 10, -30611744);
		c = ii(c, d, a, b, k[6], 15, -1560198380);
		b = ii(b, c, d, a, k[13], 21, 1309151649);
		a = ii(a, b, c, d, k[4], 6, -145523070);
		d = ii(d, a, b, c, k[11], 10, -1120210379);
		c = ii(c, d, a, b, k[2], 15, 718787259);
		b = ii(b, c, d, a, k[9], 21, -343485551);

		x[0] += a;
		x[1] += b;
		x[2] += c;
		x[3] += d;
	}

    private /*static*/ class BufferState {
        internal int[] block = new int[16];
        internal int[] state = new int[4];
    }

    private static ThreadLocal<BufferState> threadState = new ThreadLocal<BufferState>() {
        //@Override
        //public BufferState initialValue() {
        //    return new BufferState();
        //}
    };


    private static int[] md5state(byte[] key, int offset, int len) {
        int n = offset + (len & ~63);
        BufferState bufferState = threadState.Value;
        int[] block = bufferState.block;
        int[] state = bufferState.state;

        state[0] = 1732584193;
        state[1] = -271733879;
        state[2] = -1732584194;
        state[3] = 271733878;

        int i = offset;
        for (; i < n; i += 64) {
            for (int w = 0; w < 16; ++w) {
                block[w] = Buffers.GetUInt32(key, i + (w << 2));
            }

            md5cycle(state, block);
        }

        int m = len & ~3;
        int w2 = 0;
        for (; i < m; i += 4) {
            block[w2++] = Buffers.GetUInt32(key, i);
        }

        switch (len & 3) {
            case 3:
                block[w2++] = (int)(Buffers.GetUInt24(key, i) | 0x80000000);
                break;
            case 2:
                block[w2++] = Buffers.GetUInt16(key, i) | 0x800000;
                break;
            case 1:
                block[w2++] = Buffers.GetUInt8(key, i) | 0x8000;
                break;
            default:
                block[w2++] = 0x80;
                break;
        }

        if (w2 > 14) {
            if (w2 < 16) {
                block[w2] = 0;
            }

            md5cycle(state, block);
            w2 = 0;
        }

        for (; w2 < 16; ++w2) {
            block[w2] = 0;
        }

        block[14] = len << 3;
        md5cycle(state, block);
        return state;
    }




    private static byte[] Base64URLNoPaddingChars = 
    {
        Convert.ToByte('A'), Convert.ToByte('B'), Convert.ToByte('C'), Convert.ToByte('D'), Convert.ToByte('E'), 
        Convert.ToByte('F'), Convert.ToByte('G'), Convert.ToByte('H'), Convert.ToByte('I'), Convert.ToByte('J'), 
        Convert.ToByte('K'), Convert.ToByte('L'), Convert.ToByte('M'), Convert.ToByte('N'), Convert.ToByte('O'), 
        Convert.ToByte('P'), Convert.ToByte('Q'), Convert.ToByte('R'), Convert.ToByte('S'), Convert.ToByte('T'), 
        Convert.ToByte('U'), Convert.ToByte('V'), Convert.ToByte('W'), Convert.ToByte('X'), Convert.ToByte('Y'), Convert.ToByte('Z'), 
        Convert.ToByte('a'), Convert.ToByte('b'), Convert.ToByte('c'), Convert.ToByte('d'), Convert.ToByte('e'), 
        Convert.ToByte('f'), Convert.ToByte('g'), Convert.ToByte('h'), Convert.ToByte('i'), Convert.ToByte('j'),
        Convert.ToByte('k'), Convert.ToByte('l'), Convert.ToByte('m'), Convert.ToByte('n'), Convert.ToByte('o'), 
        Convert.ToByte('p'), Convert.ToByte('q'), Convert.ToByte('r'), Convert.ToByte('s'), Convert.ToByte('t'), 
        Convert.ToByte('u'), Convert.ToByte('v'), Convert.ToByte('w'), Convert.ToByte('x'), Convert.ToByte('y'), Convert.ToByte('z'), 
        Convert.ToByte('0'), Convert.ToByte('1'), Convert.ToByte('2'), Convert.ToByte('3'), Convert.ToByte('4'), 
        Convert.ToByte('5'), Convert.ToByte('6'), Convert.ToByte('7'), Convert.ToByte('8'), Convert.ToByte('9'), 
        Convert.ToByte('-'), Convert.ToByte('_')
    };


    //private static byte[] Base64URLNoPaddingChars = new byte[]{'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
    //    'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g',
    //    'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1',
    //    '2', '3', '4', '5', '6', '7', '8', '9', '-', '_'};
}