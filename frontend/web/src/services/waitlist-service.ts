import axios from 'axios'
import { config } from '../config'
import { authConfig } from '../shared/http'

export interface WaitlistEntryInput {
  fullName: string
  phone: string
  email: string
}

export interface WaitlistEntryResult {
  id: string
  email: string
}

const COUNTRY_CODE = '+57'

class WaitlistService {
  async register(input: WaitlistEntryInput): Promise<WaitlistEntryResult> {
    const { data } = await axios.post<WaitlistEntryResult>(
      `${config.API_URL}/api/waitlist`,
      {
        fullName: input.fullName,
        countryCode: COUNTRY_CODE,
        phone: input.phone,
        email: input.email,
      },
      authConfig(),
    )

    return data
  }
}

export default WaitlistService
