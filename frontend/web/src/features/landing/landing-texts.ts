import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../styles/components/texts'

export const LandingSectionHead = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  max-width: 720px;
  margin: 0 auto 56px;
  text-align: center;
`

export const LandingSectionTitle = styled(SectionTitle)`
  max-width: 720px;
  margin: 0;
  font-size: clamp(28px, 4vw, 44px);
  font-weight: 600;
  line-height: 1.1;
  letter-spacing: -0.025em;
  text-align: center;

  @media (max-width: 600px) {
    font-size: 25px;
  }
`

export const LandingLead = styled(MutedText)`
  max-width: 720px;
  margin: 0;
  text-align: center;
  font-size: 17px;
  line-height: 1.6;

  @media (max-width: 600px) {
    font-size: 16px;
  }
`
