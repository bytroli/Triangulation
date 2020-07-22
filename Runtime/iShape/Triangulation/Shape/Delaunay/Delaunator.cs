﻿using Unity.Collections;
using iShape.Geometry;
using iShape.Collections;

namespace iShape.Triangulation.Shape.Delaunay {

    public struct Delaunator {
        private int pathCount;
        private int extraCount;
        private NativeArray<Triangle> triangles;

        public NativeArray<int> Indices(Allocator allocator) {
            int n = triangles.Length;
            var result = new NativeArray<int>(3 * n, allocator);
            int i = 0;
            int j = 0;
            do {
                var triangle = this.triangles[i];
                result[j] = triangle.vA.index;
                result[j + 1] = triangle.vB.index;
                result[j + 2] = triangle.vC.index;

                j += 3;
                i += 1;
            } while(i < n);

            return result;
        }

        public Delaunator(int pathCount, int extraCount, NativeArray<Triangle> triangles) {
            this.pathCount = pathCount;
            this.extraCount = extraCount;
            this.triangles = triangles;
        }

        public void Build() {
            int count = triangles.Length;
            var visitMarks = new NativeArray<bool>(count, Allocator.Temp);
            var visitIndex = 0;

            var origin = new DynamicArray<int>(16, Allocator.Temp);
            var buffer = new DynamicArray<int>(16, Allocator.Temp);

            origin.Add(0);

            while(origin.Count > 0) {
                buffer.RemoveAll();
                for(int l = 0; l < origin.Count; ++l) {
                    int i = origin[l];
                    var triangle = this.triangles[i];
                    visitMarks[i] = true;

                    for(int k = 0; k < 3; ++k) {
                        
                        int neighborIndex = triangle.GetNeighborByIndex(k);
                        if(neighborIndex >= 0) {
                            var neighbor = triangles[neighborIndex];
                            if(this.Swap(triangle, neighbor)) {

                                triangle = this.triangles[triangle.index];
                                neighbor = this.triangles[neighbor.index];

                                for(int j = 0; j < 3; ++j) {
                                    int ni = triangle.GetNeighborByIndex(j);
                                    if(ni >= 0 && ni != neighbor.index) {
                                        buffer.Add(ni);
                                    }
                                }

                                for(int j = 0; j < 3; ++j) {
                                    int ni = neighbor.GetNeighborByIndex(j);
                                    if(ni >= 0 && ni != triangle.index) {
                                        buffer.Add(ni);
                                    }
                                }
                            }
                        }
                    }
                }
                origin.RemoveAll();
                
                if(buffer.Count == 0 && visitIndex < count) {
                    ++visitIndex;
                    while(visitIndex < count) {
                        if(visitMarks[visitIndex] == false) {
                            origin.Add(visitIndex);
                            break;
                        }
                        ++visitIndex;
                    }
                } else {
                    origin.Add(buffer);   
                }
            }

			origin.Dispose();
			buffer.Dispose();
            visitMarks.Dispose();
		}

        public int Fix(NativeArray<int> indices)  {
            int count = triangles.Length;

            int minFixIndex = count;
            
            var origin = new DynamicArray<int>(indices, Allocator.Temp);
            var buffer = new DynamicArray<int>(16, Allocator.Temp);

            while(origin.Count > 0) {
                buffer.RemoveAll();
                for(int l = 0; l < origin.Count; ++l) {
                    int i = origin[l];
                    var triangle = this.triangles[i];

                    for(int k = 0; k < 3; ++k) {
                        
                        int neighborIndex = triangle.GetNeighborByIndex(k);
                        if(neighborIndex >= 0) {
                            var neighbor = triangles[neighborIndex];
                            if(this.Swap(triangle, neighbor)) {

                                if (minFixIndex > neighborIndex) {
                                    minFixIndex = neighborIndex;
                                }
                                if (minFixIndex > i) {
                                    minFixIndex = i;
                                }
                                
                                triangle = this.triangles[triangle.index];
                                neighbor = this.triangles[neighbor.index];

                                for(int j = 0; j < 3; ++j) {
                                    int ni = triangle.GetNeighborByIndex(j);
                                    if(ni >= 0 && ni != neighbor.index) {
                                        buffer.Add(ni);
                                    }
                                }

                                for(int j = 0; j < 3; ++j) {
                                    int ni = neighbor.GetNeighborByIndex(j);
                                    if(ni >= 0 && ni != triangle.index) {
                                        buffer.Add(ni);
                                    }
                                }
                            }
                        }
                    }
                }
                origin.RemoveAll();
                origin.Add(buffer);
            }

			origin.Dispose();
			buffer.Dispose();

            return minFixIndex;
        }

        private bool Swap(Triangle abc, Triangle pbc) {
            int ai = abc.Opposite(pbc.index);    // opposite a-p
            int bi;                              // edge bc
            int ci;

            Vertex a, b, c;

            int acIndex;
            
            switch (ai) {
            case 0: 
                bi = 1;
                ci = 2;
                a = abc.vA;
                b = abc.vB;
                c = abc.vC;

                acIndex = abc.nB;
                break;
            case 1:
                bi = 2;
                ci = 0;
                a = abc.vB;
                b = abc.vC;
                c = abc.vA;
                
                acIndex = abc.nC;
                break;
            default:
                bi = 0;
                ci = 1;
                a = abc.vC;
                b = abc.vA;
                c = abc.vB;
                
                acIndex = abc.nA;
                break;
            }

            var p = pbc.oppositeVertex(abc.index);

            bool isPrefect = IsPrefect(p.point, c.point, a.point, b.point);

            if(isPrefect) {
                return false;
            }

            bool isABP_CCW = IsCCW(a.point, b.point, p.point);

            int bp = pbc.GetNeighborByVertex(c.index);
            int cp = pbc.GetNeighborByVertex(b.index);
            int ab = abc.GetNeighborByIndex(ci);
            int ac = abc.GetNeighborByIndex(bi);

            // abc -> abp
            Triangle abp;

            // pbc -> acp
            Triangle acp;

            if(isABP_CCW) {
                abp = new Triangle(abc.index, a, b, p) {
                    nA = bp,            // a - bp
                    nB = pbc.index,     // b - ap
                    nC = ab             // p - ab
                };

                acp = new Triangle(pbc.index, a, p, c) {
                    nA = cp,            // a - cp
                    nB = ac,            // p - ac
                    nC = abc.index      // c - ap
                };
            } else {
                abp = new Triangle(abc.index, a, p, b) {
                    nA = bp,            // a - bp
                    nB = ab,            // p - ab
                    nC = pbc.index      // b - ap
                };

                acp = new Triangle(pbc.index, a, c, p) {
                    nA = cp,            // a - cp
                    nB = abc.index,     // c - ap
                    nC = ac             // p - ac
                };
            }

            // fix neighbor's link
            // ab, cp didn't change neighbor
            // bc -> ap, so no changes

            // ac (abc) is now edge of acp
            // int acIndex = abc.GetNeighborByIndex(bi); // b - angle
            if(acIndex >= 0) {
                var neighbor = this.triangles[acIndex];
                neighbor.UpdateOpposite(abc.index, acp.index);
                this.triangles[acIndex] = neighbor;
            }

            // bp (pbc) is now edge of abp
            int bpIndex = pbc.GetNeighborByVertex(c.index); // c - angle
            if(bpIndex >= 0) {
                var neighbor = this.triangles[bpIndex];
                neighbor.UpdateOpposite(pbc.index, abp.index);
                this.triangles[bpIndex] = neighbor;
            }

            this.triangles[abc.index] = abp;
            this.triangles[pbc.index] = acp;

            return true;
        }

        private static bool IsPrefect(IntVector p, IntVector a, IntVector b, IntVector c) {
            bool isPABccw = IsCCW(p, a, b);
            bool isPCBccw = IsCCW(p, c, b);
            return isPABccw == isPCBccw || IsDelaunay(p, a, b, c);
        }

        private static bool IsDelaunay(IntVector p, IntVector a, IntVector b, IntVector c) {

            long bax = a.x - b.x;
            long bay = a.y - b.y;
            long bcx = c.x - b.x;
            long bcy = c.y - b.y;

            long pcx = c.x - p.x;
            long pcy = c.y - p.y;
            long pax = a.x - p.x;
            long pay = a.y - p.y;

            long cosAlpha = pax * pcx + pay * pcy;
            long cosBeta = bax * bcx + bay * bcy;
            long sinAlpha = pay * pcx - pax * pcy;
            long sinBeta = bax * bcy - bay * bcx;
            // TODO think about this constant
            return (float)sinAlpha * (float)cosBeta + (float)cosAlpha * (float)sinBeta >= -1000000000;
        }

        private static bool IsCCW(IntVector a, IntVector b, IntVector c) {
            long m0 = (c.y - a.y) * (b.x - a.x);
            long m1 = (b.y - a.y) * (c.x - a.x);

            return m0 < m1;
        }

    }
}
