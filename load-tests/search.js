import http from 'k6/http';
import { check } from 'k6';

// 20 virtual users for 30 seconds
export const options = { vus: 20, duration: '30s' };

const baseUrl = __ENV.BASE_URL || 'http://localhost:5080';

export default function () {
  const res = http.get(`${baseUrl}/api/v1/listings?page=1&pageSize=20`);
  check(res, { 'status is 200': (r) => r.status === 200 });
}
